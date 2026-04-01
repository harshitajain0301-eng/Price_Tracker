using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.IO;
using System.Threading.Tasks;
using PriceDropCatcher.Models;

namespace PriceDropCatcher.Data
{
    public class SqliteDatabaseService
    {
        private readonly string _connectionString;

        public SqliteDatabaseService()
        {
            var dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "PriceDropCatcher");
            Directory.CreateDirectory(dir);
            var dbPath = Path.Combine(dir, "pricedrop.db");
            _connectionString = "Data Source=" + dbPath + ";Version=3;";
        }

        public void Initialize()
        {
            using (var conn = Open())
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = @"
CREATE TABLE IF NOT EXISTS search_history (
  id INTEGER PRIMARY KEY AUTOINCREMENT,
  term TEXT NOT NULL,
  created_at TEXT NOT NULL
);
CREATE TABLE IF NOT EXISTS viewed_products (
  id INTEGER PRIMARY KEY AUTOINCREMENT,
  url TEXT NOT NULL,
  name TEXT NOT NULL,
  created_at TEXT NOT NULL
);
CREATE TABLE IF NOT EXISTS tracked_products (
  id INTEGER PRIMARY KEY AUTOINCREMENT,
  url TEXT NOT NULL,
  name TEXT NOT NULL,
  threshold REAL,
  last_min_price REAL,
  last_check TEXT,
  created_at TEXT NOT NULL
);
CREATE TABLE IF NOT EXISTS price_snapshots (
  id INTEGER PRIMARY KEY AUTOINCREMENT,
  tracked_id INTEGER NOT NULL,
  min_price REAL,
  avg_price REAL,
  captured_at TEXT NOT NULL,
  FOREIGN KEY(tracked_id) REFERENCES tracked_products(id)
);
CREATE INDEX IF NOT EXISTS idx_tracked_name ON tracked_products(name);
";
                cmd.ExecuteNonQuery();
            }
        }

        private SQLiteConnection Open()
        {
            var c = new SQLiteConnection(_connectionString);
            c.Open();
            return c;
        }

        public void InsertSearchHistory(string term)
        {
            if (string.IsNullOrWhiteSpace(term)) return;
            using (var conn = Open())
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = "INSERT INTO search_history(term, created_at) VALUES(@t, @c)";
                cmd.Parameters.AddWithValue("@t", term.Trim());
                cmd.Parameters.AddWithValue("@c", DateTime.UtcNow.ToString("o"));
                cmd.ExecuteNonQuery();
            }
        }

        public void InsertViewedProduct(string url, string name)
        {
            if (string.IsNullOrWhiteSpace(url)) return;
            using (var conn = Open())
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = "INSERT INTO viewed_products(url, name, created_at) VALUES(@u, @n, @c)";
                cmd.Parameters.AddWithValue("@u", url.Trim());
                cmd.Parameters.AddWithValue("@n", (name ?? "").Trim());
                cmd.Parameters.AddWithValue("@c", DateTime.UtcNow.ToString("o"));
                cmd.ExecuteNonQuery();
            }
        }

        public long SaveTrackedProduct(string url, string name, decimal? threshold)
        {
            using (var conn = Open())
            {
                long existingId = 0;
                using (var find = conn.CreateCommand())
                {
                    find.CommandText = "SELECT id FROM tracked_products WHERE url=@u LIMIT 1";
                    find.Parameters.AddWithValue("@u", url ?? "");
                    var o = find.ExecuteScalar();
                    if (o != null && o != DBNull.Value) existingId = Convert.ToInt64(o);
                }

                if (existingId > 0)
                {
                    using (var up = conn.CreateCommand())
                    {
                        up.CommandText = @"
UPDATE tracked_products SET name=@n, threshold=@th WHERE id=@id";
                        up.Parameters.AddWithValue("@n", name ?? "");
                        up.Parameters.AddWithValue("@th", threshold.HasValue ? (object)(double)threshold.Value : DBNull.Value);
                        up.Parameters.AddWithValue("@id", existingId);
                        up.ExecuteNonQuery();
                    }
                    return existingId;
                }

                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = @"
INSERT INTO tracked_products(url, name, threshold, created_at)
VALUES(@u, @n, @th, @c);
SELECT last_insert_rowid();";
                    cmd.Parameters.AddWithValue("@u", url ?? "");
                    cmd.Parameters.AddWithValue("@n", name ?? "");
                    cmd.Parameters.AddWithValue("@th", threshold.HasValue ? (object)(double)threshold.Value : DBNull.Value);
                    cmd.Parameters.AddWithValue("@c", DateTime.UtcNow.ToString("o"));
                    return Convert.ToInt64(cmd.ExecuteScalar());
                }
            }
        }

        public void UpdateTrackedPriceState(long id, decimal? lastMin, DateTime utc)
        {
            using (var conn = Open())
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = @"
UPDATE tracked_products SET last_min_price=@m, last_check=@c WHERE id=@id";
                cmd.Parameters.AddWithValue("@m", lastMin.HasValue ? (object)(double)lastMin.Value : DBNull.Value);
                cmd.Parameters.AddWithValue("@c", utc.ToString("o"));
                cmd.Parameters.AddWithValue("@id", id);
                cmd.ExecuteNonQuery();
            }
        }

        public void UpdateTrackedThreshold(long id, decimal? threshold)
        {
            using (var conn = Open())
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = "UPDATE tracked_products SET threshold=@t WHERE id=@id";
                cmd.Parameters.AddWithValue("@t", threshold.HasValue ? (object)(double)threshold.Value : DBNull.Value);
                cmd.Parameters.AddWithValue("@id", id);
                cmd.ExecuteNonQuery();
            }
        }

        public void InsertPriceSnapshot(long trackedId, decimal? min, decimal? avg, DateTime utc)
        {
            using (var conn = Open())
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = @"
INSERT INTO price_snapshots(tracked_id, min_price, avg_price, captured_at)
VALUES(@id, @min, @avg, @c)";
                cmd.Parameters.AddWithValue("@id", trackedId);
                cmd.Parameters.AddWithValue("@min", min.HasValue ? (object)(double)min.Value : DBNull.Value);
                cmd.Parameters.AddWithValue("@avg", avg.HasValue ? (object)(double)avg.Value : DBNull.Value);
                cmd.Parameters.AddWithValue("@c", utc.ToString("o"));
                cmd.ExecuteNonQuery();
            }
        }

        public List<TrackedProduct> GetTrackedProducts()
        {
            var list = new List<TrackedProduct>();
            using (var conn = Open())
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = @"
SELECT id, url, name, threshold, last_min_price, last_check, created_at
FROM tracked_products ORDER BY id DESC";
                using (var r = cmd.ExecuteReader())
                {
                    while (r.Read())
                    {
                        list.Add(new TrackedProduct
                        {
                            Id = r.GetInt64(0),
                            Url = r.IsDBNull(1) ? null : r.GetString(1),
                            Name = r.IsDBNull(2) ? null : r.GetString(2),
                            Threshold = r.IsDBNull(3) ? (decimal?)null : Convert.ToDecimal(r.GetValue(3)),
                            LastMinPrice = r.IsDBNull(4) ? (decimal?)null : Convert.ToDecimal(r.GetValue(4)),
                            LastCheckedUtc = r.IsDBNull(5) ? (DateTime?)null : DateTime.Parse(r.GetString(5), null, System.Globalization.DateTimeStyles.RoundtripKind),
                            CreatedAtUtc = r.IsDBNull(6) ? DateTime.UtcNow : DateTime.Parse(r.GetString(6), null, System.Globalization.DateTimeStyles.RoundtripKind)
                        });
                    }
                }
            }
            return list;
        }

        public List<(DateTime Time, decimal? Min, decimal? Avg)> GetSnapshotsForTracked(long trackedId, int maxPoints = 24)
        {
            var list = new List<(DateTime, decimal?, decimal?)>();
            using (var conn = Open())
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = @"
SELECT captured_at, min_price, avg_price FROM price_snapshots
WHERE tracked_id=@id ORDER BY id DESC LIMIT @lim";
                cmd.Parameters.AddWithValue("@id", trackedId);
                cmd.Parameters.AddWithValue("@lim", maxPoints);
                using (var r = cmd.ExecuteReader())
                {
                    while (r.Read())
                    {
                        var t = DateTime.Parse(r.GetString(0), null, System.Globalization.DateTimeStyles.RoundtripKind);
                        decimal? min = r.IsDBNull(1) ? (decimal?)null : Convert.ToDecimal(r.GetValue(1));
                        decimal? avg = r.IsDBNull(2) ? (decimal?)null : Convert.ToDecimal(r.GetValue(2));
                        list.Add((t, min, avg));
                    }
                }
            }
            list.Reverse();
            return list;
        }

        public Task InitializeAsync() => Task.Run((Action)Initialize);
    }
}
