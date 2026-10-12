using Microsoft.Data.Sqlite;
using WinActivityTracker.Config;

namespace WinActivityTracker.Data;

/// <summary>
/// Доступ к SQLite. Данные агрегируются в почасовые корзины (день, час, процесс):
/// база не растёт построчно на каждый тик, и легко получить и шкалу по часам,
/// и сводки за день/неделю/месяц.
/// </summary>
public sealed class Database
{
    private readonly string _connectionString;
    private readonly object _writeGate = new();

    public Database()
    {
        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = AppPaths.DatabaseFile,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Shared,
        }.ToString();
    }

    private SqliteConnection Open()
    {
        var conn = new SqliteConnection(_connectionString);
        conn.Open();
        using (var pragma = conn.CreateCommand())
        {
            pragma.CommandText = "PRAGMA journal_mode=WAL; PRAGMA busy_timeout=5000; PRAGMA synchronous=NORMAL;";
            pragma.ExecuteNonQuery();
        }
        return conn;
    }

    public void Initialize()
    {
        using var conn = Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            CREATE TABLE IF NOT EXISTS activity (
                day      TEXT    NOT NULL,   -- 'YYYY-MM-DD' локального времени
                hour     INTEGER NOT NULL,   -- 0..23
                process  TEXT    NOT NULL,   -- имя exe, нижний регистр
                app_name TEXT    NOT NULL,   -- дружелюбное имя
                category TEXT    NOT NULL,   -- 'Game' | 'Application'
                seconds  INTEGER NOT NULL DEFAULT 0,
                PRIMARY KEY (day, hour, process)
            );
            CREATE INDEX IF NOT EXISTS idx_activity_day ON activity (day);

            CREATE TABLE IF NOT EXISTS screen (
                day     TEXT    NOT NULL,
                hour    INTEGER NOT NULL,
                seconds INTEGER NOT NULL DEFAULT 0,
                PRIMARY KEY (day, hour)
            );

            CREATE TABLE IF NOT EXISTS sessions (
                id             INTEGER PRIMARY KEY AUTOINCREMENT,
                process        TEXT    NOT NULL,
                app_name       TEXT    NOT NULL,
                category       TEXT    NOT NULL,
                start_time     TEXT    NOT NULL,   -- 'yyyy-MM-dd HH:mm:ss' локального времени
                end_time       TEXT    NOT NULL,
                active_seconds INTEGER NOT NULL DEFAULT 0
            );
            CREATE INDEX IF NOT EXISTS idx_sessions_start ON sessions (start_time);
            CREATE INDEX IF NOT EXISTS idx_sessions_proc ON sessions (process, end_time);
            """;
        cmd.ExecuteNonQuery();
        Logger.Info($"База инициализирована: {AppPaths.DatabaseFile}");
    }

    /// <summary>Добавить секунды активности в корзину текущего часа.</summary>
    public void AddSeconds(string process, string appName, string category, int seconds, DateTime localNow)
    {
        if (seconds <= 0) return;
        var day = localNow.ToString("yyyy-MM-dd");
        var hour = localNow.Hour;

        lock (_writeGate)
        {
            using var conn = Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = """
                INSERT INTO activity (day, hour, process, app_name, category, seconds)
                VALUES ($day, $hour, $process, $app, $cat, $sec)
                ON CONFLICT(day, hour, process) DO UPDATE SET
                    seconds  = seconds + excluded.seconds,
                    app_name = excluded.app_name,
                    category = excluded.category;
                """;
            cmd.Parameters.AddWithValue("$day", day);
            cmd.Parameters.AddWithValue("$hour", hour);
            cmd.Parameters.AddWithValue("$process", process);
            cmd.Parameters.AddWithValue("$app", appName);
            cmd.Parameters.AddWithValue("$cat", category);
            cmd.Parameters.AddWithValue("$sec", seconds);
            cmd.ExecuteNonQuery();
        }
    }

    /// <summary>Сменить категорию всех записей процесса (для ручной переклассификации).</summary>
    public void SetCategory(string process, string category)
    {
        lock (_writeGate)
        {
            using var conn = Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "UPDATE activity SET category = $cat WHERE process = $p;";
            cmd.Parameters.AddWithValue("$cat", category);
            cmd.Parameters.AddWithValue("$p", process);
            cmd.ExecuteNonQuery();
        }
    }

    /// <summary>Удалить все записи процесса (когда помечаем «не учитывать»).</summary>
    public void DeleteProcess(string process)
    {
        lock (_writeGate)
        {
            using var conn = Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "DELETE FROM activity WHERE process = $p;";
            cmd.Parameters.AddWithValue("$p", process);
            cmd.ExecuteNonQuery();
        }
    }

    /// <summary>Сбросить WAL в основной файл базы (вызывать при выходе для надёжности).</summary>
    public void Checkpoint()
    {
        try
        {
            using var conn = Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "PRAGMA wal_checkpoint(TRUNCATE);";
            cmd.ExecuteNonQuery();
        }
        catch (Exception ex)
        {
            Logger.Error("Checkpoint базы не удался", ex);
        }
    }

    /// <summary>Добавить секунды «экран активен» (любое окно, лишь бы не блокировка/сон).</summary>
    public void AddScreenSeconds(int seconds, DateTime localNow)
    {
        if (seconds <= 0) return;
        var day = localNow.ToString("yyyy-MM-dd");
        var hour = localNow.Hour;
        lock (_writeGate)
        {
            using var conn = Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = """
                INSERT INTO screen (day, hour, seconds) VALUES ($day, $hour, $sec)
                ON CONFLICT(day, hour) DO UPDATE SET seconds = seconds + excluded.seconds;
                """;
            cmd.Parameters.AddWithValue("$day", day);
            cmd.Parameters.AddWithValue("$hour", hour);
            cmd.Parameters.AddWithValue("$sec", seconds);
            cmd.ExecuteNonQuery();
        }
    }

    private const string TimeFmt = "yyyy-MM-dd HH:mm:ss";

    /// <summary>
    /// Записать активность в сеанс: продолжить последний сеанс приложения, если вернулись
    /// в пределах gapSeconds (склейка альт-табов и коротких отлучек), иначе начать новый.
    /// </summary>
    public void RecordSession(string process, string appName, string category, int seconds, DateTime now, int gapSeconds)
    {
        if (seconds <= 0) return;
        lock (_writeGate)
        {
            using var conn = Open();
            long? id = null;
            DateTime lastEnd = default;
            using (var q = conn.CreateCommand())
            {
                q.CommandText = "SELECT id, end_time FROM sessions WHERE process=$p ORDER BY end_time DESC LIMIT 1;";
                q.Parameters.AddWithValue("$p", process);
                using var r = q.ExecuteReader();
                if (r.Read())
                {
                    id = r.GetInt64(0);
                    DateTime.TryParse(r.GetString(1), System.Globalization.CultureInfo.InvariantCulture,
                        System.Globalization.DateTimeStyles.None, out lastEnd);
                }
            }

            var canExtend = id.HasValue && (now - lastEnd).TotalSeconds <= gapSeconds && (now - lastEnd).TotalSeconds >= 0;
            using var cmd = conn.CreateCommand();
            if (canExtend)
            {
                cmd.CommandText = """
                    UPDATE sessions SET end_time=$end, active_seconds=active_seconds+$sec,
                        app_name=$app, category=$cat WHERE id=$id;
                    """;
                cmd.Parameters.AddWithValue("$id", id!.Value);
            }
            else
            {
                cmd.CommandText = """
                    INSERT INTO sessions (process, app_name, category, start_time, end_time, active_seconds)
                    VALUES ($p, $app, $cat, $start, $end, $sec);
                    """;
                cmd.Parameters.AddWithValue("$p", process);
                cmd.Parameters.AddWithValue("$start", now.AddSeconds(-seconds).ToString(TimeFmt));
            }
            cmd.Parameters.AddWithValue("$end", now.ToString(TimeFmt));
            cmd.Parameters.AddWithValue("$sec", seconds);
            cmd.Parameters.AddWithValue("$app", appName);
            cmd.Parameters.AddWithValue("$cat", category);
            cmd.ExecuteNonQuery();
        }
    }

    /// <summary>Сеансы за диапазон дат (по началу), новые сверху.</summary>
    public List<SessionRecord> GetSessions(DateOnly from, DateOnly to, int limit = 500)
    {
        using var conn = Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT process, app_name, category, start_time, end_time, active_seconds
            FROM sessions
            WHERE DATE(start_time) BETWEEN $from AND $to
            ORDER BY start_time DESC
            LIMIT $limit;
            """;
        cmd.Parameters.AddWithValue("$from", from.ToString("yyyy-MM-dd"));
        cmd.Parameters.AddWithValue("$to", to.ToString("yyyy-MM-dd"));
        cmd.Parameters.AddWithValue("$limit", limit);

        var list = new List<SessionRecord>();
        using var r = cmd.ExecuteReader();
        while (r.Read())
        {
            DateTime.TryParse(r.GetString(3), System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var st);
            DateTime.TryParse(r.GetString(4), System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var en);
            list.Add(new SessionRecord(r.GetString(0), r.GetString(1), r.GetString(2), st, en, r.GetInt64(5)));
        }
        return list;
    }

    /// <summary>Суммарное время работы экрана за диапазон дат.</summary>
    public long GetScreenSeconds(DateOnly from, DateOnly to)
    {
        using var conn = Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT COALESCE(SUM(seconds),0) FROM screen WHERE day BETWEEN $from AND $to;";
        cmd.Parameters.AddWithValue("$from", from.ToString("yyyy-MM-dd"));
        cmd.Parameters.AddWithValue("$to", to.ToString("yyyy-MM-dd"));
        return Convert.ToInt64(cmd.ExecuteScalar() ?? 0L);
    }

    /// <summary>Сводка по процессам за диапазон дат (включительно), по убыванию времени.</summary>
    public List<ProcessTotal> GetTotals(DateOnly from, DateOnly to)
    {
        using var conn = Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT process, app_name, category, SUM(seconds) AS total
            FROM activity
            WHERE day BETWEEN $from AND $to
            GROUP BY process
            ORDER BY total DESC;
            """;
        cmd.Parameters.AddWithValue("$from", from.ToString("yyyy-MM-dd"));
        cmd.Parameters.AddWithValue("$to", to.ToString("yyyy-MM-dd"));

        var result = new List<ProcessTotal>();
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            result.Add(new ProcessTotal(
                reader.GetString(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetInt64(3)));
        }
        return result;
    }

    /// <summary>Почасовая шкала за один день: 24 корзины, игры и приложения отдельно.</summary>
    public List<HourlyBucket> GetHourly(DateOnly day)
    {
        using var conn = Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT hour,
                   SUM(CASE WHEN category = 'Game' THEN seconds ELSE 0 END) AS game,
                   SUM(CASE WHEN category != 'Game' THEN seconds ELSE 0 END) AS app
            FROM activity
            WHERE day = $day
            GROUP BY hour;
            """;
        cmd.Parameters.AddWithValue("$day", day.ToString("yyyy-MM-dd"));

        var map = new Dictionary<int, HourlyBucket>();
        using (var reader = cmd.ExecuteReader())
        {
            while (reader.Read())
            {
                var h = reader.GetInt32(0);
                map[h] = new HourlyBucket(h, reader.GetInt64(1), reader.GetInt64(2));
            }
        }

        var buckets = new List<HourlyBucket>(24);
        for (var h = 0; h < 24; h++)
            buckets.Add(map.TryGetValue(h, out var b) ? b : new HourlyBucket(h, 0, 0));
        return buckets;
    }
}
