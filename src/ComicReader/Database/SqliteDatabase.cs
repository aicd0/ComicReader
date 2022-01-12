using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Serialization;
using Windows.Storage;
using Windows.Storage.Streams;

namespace ComicReader.Database
{
    public class SqlKey
    {
        public SqlKey(string name, object value = null, bool blob = false)
        {
            Name = name;
            Value = value;
            IsBlob = blob;
        }

        public string Name;
        public object Value;
        public bool IsBlob;
    }

    public class SqliteDatabaseManager
    {
        public static string ComicTable = "comics";
        public static string TagCategoryTable = "tag_categories";
        public static string TagTable = "tags";

        private static StorageFolder DatabaseFolder => ApplicationData.Current.LocalFolder;
        private static string DatabaseFileName => "database.db";
        private static string DatabasePath => Path.Combine(DatabaseFolder.Path, DatabaseFileName);

        private static SqliteConnection m_connection = null;

        public static async Task Init()
        {
            // Create database.
            await DatabaseFolder.CreateFileAsync(DatabaseFileName, CreationCollisionOption.OpenIfExists);

            // Build connection.
            SqliteConnection connection =
                new SqliteConnection($"Filename={DatabasePath}");
            connection.Open();
            SqliteCommand command = connection.CreateCommand();

            // Create comic table.
            command.CommandText = "CREATE TABLE IF NOT EXISTS " + ComicTable + " (" +
                ComicData.Field.Id + " INTEGER PRIMARY KEY AUTOINCREMENT," + // 0
                ComicData.Field.Title1 + " TEXT," + // 1
                ComicData.Field.Title2 + " TEXT," + // 2
                ComicData.Field.Directory + " TEXT NOT NULL," + // 3
                ComicData.Field.Hidden + " BOOLEAN NOT NULL," + // 4
                ComicData.Field.Rating + " INTEGER NOT NULL," + // 5
                ComicData.Field.Progress + " INTEGER NOT NULL," + // 6
                ComicData.Field.LastVisit + " TIMESTAMP NOT NULL," + // 7
                ComicData.Field.LastPosition + " REAL NOT NULL," + // 8
                ComicData.Field.CoverFileName + " TEXT," + // 9
                ComicData.Field.ImageAspectRatios + " BLOB)"; // 10
            command.ExecuteNonQuery();

            // Create tag category table.
            command.CommandText = "CREATE TABLE IF NOT EXISTS " + TagCategoryTable + " (" +
                ComicData.Field.TagCategory.Id + " INTEGER PRIMARY KEY AUTOINCREMENT," + // 0
                ComicData.Field.TagCategory.Name + " TEXT," + // 1
                ComicData.Field.TagCategory.ComicId + " INTEGER REFERENCES " + ComicTable + "(" + ComicData.Field.Id + ") ON DELETE CASCADE)"; // 2
            command.ExecuteNonQuery();

            // Create tag table.
            command.CommandText = "CREATE TABLE IF NOT EXISTS " + TagTable + " (" +
                ComicData.Field.Tag.Content + " TEXT," + // 0
                ComicData.Field.Tag.ComicId + " INTEGER NOT NULL," + // 1
                ComicData.Field.Tag.TagCategoryId + " INTEGER REFERENCES " + TagCategoryTable + "(" + ComicData.Field.TagCategory.Id + ") ON DELETE CASCADE)"; // 2
            command.ExecuteNonQuery();
            
            // Cleanups.
            command.Dispose();

            // Enable database.
            m_connection = connection;
        }

        public static SqliteCommand NewCommand()
        {
            System.Diagnostics.Debug.Assert(m_connection != null);
            return m_connection.CreateCommand();
        }

        public static async Task<long> Insert(LockContext db, string table, List<SqlKey> keys)
        {
            List<string> field_names = new List<string>();
            List<string> field_vals = new List<string>();
            var blobs = new List<KeyValuePair<string, MemoryStream>>();
            SqliteCommand command = NewCommand();

            foreach (SqlKey key in keys)
            {
                field_names.Add(key.Name);

                if (key.IsBlob)
                {
                    MemoryStream stream = Utils.C0.SerializeToMemoryStream(key.Value);
                    blobs.Add(new KeyValuePair<string, MemoryStream>(key.Name, stream));

                    string param = "$len_" + key.Name;
                    field_vals.Add("zeroblob(" + param + ")");
                    command.Parameters.AddWithValue(param, stream.Length);
                }
                else
                {
                    string param = "@" + key.Name;
                    field_vals.Add(param);
                    command.Parameters.AddWithValue(param, key.Value);
                }
            }

            command.CommandText = "INSERT INTO " + table + " (" +
                string.Join(',', field_names) + ") VALUES (" +
                string.Join(',', field_vals) + ");" +
                "SELECT LAST_INSERT_ROWID();";

            await ComicDataManager.WaitLock(db); // Lock on.
            long rowid = (long)command.ExecuteScalar();
            command.Dispose();

            // Copy to blobs.
            foreach (var pairs in blobs)
            {
                MemoryStream input_stream = pairs.Value;
                input_stream.Seek(0, SeekOrigin.Begin);

                using (SqliteBlob write_stream = new SqliteBlob(
                    m_connection, table, pairs.Key, rowid))
                {
                    await input_stream.CopyToAsync(write_stream);
                }
            }
            ComicDataManager.ReleaseLock(db); // Lock off.

            return rowid;
        }

        public static async Task Update(LockContext db, string table, SqlKey primary_key, List<SqlKey> keys)
        {
            if (keys.Count == 0)
            {
                return;
            }

            List<string> fields = new List<string>();
            var blobs = new List<KeyValuePair<string, MemoryStream>>();
            SqliteCommand command = NewCommand();
            command.Parameters.AddWithValue("@" + primary_key.Name, primary_key.Value);

            foreach (SqlKey key in keys)
            {
                if (key.IsBlob)
                {
                    MemoryStream stream = Utils.C0.SerializeToMemoryStream(key.Value);
                    blobs.Add(new KeyValuePair<string, MemoryStream>(key.Name, stream));

                    string param = "$len_" + key.Name;
                    fields.Add(key.Name + "=zeroblob(" + param + ")");
                    command.Parameters.AddWithValue(param, stream.Length);
                }
                else
                {
                    string param = "@" + key.Name;
                    fields.Add(key.Name + "=" + param);
                    command.Parameters.AddWithValue(param, key.Value);
                }
            }

            string condition = " WHERE " + primary_key.Name + "=@" + primary_key.Name;
            command.CommandText = "UPDATE " + table + " SET " +
                string.Join(',', fields) + condition;

            await ComicDataManager.WaitLock(db); // Lock on.
            command.ExecuteNonQuery();

            // Copy to blobs.
            if (blobs.Count > 0)
            {
                command.CommandText = "SELECT rowid FROM " + table + condition + " LIMIT 1";
                long rowid = (long)command.ExecuteScalar();

                foreach (var pairs in blobs)
                {
                    MemoryStream input_stream = pairs.Value;
                    input_stream.Seek(0, SeekOrigin.Begin);

                    using (SqliteBlob write_stream = new SqliteBlob(
                        m_connection, table, pairs.Key, rowid))
                    {
                        await input_stream.CopyToAsync(write_stream);
                    }
                }
            }
            ComicDataManager.ReleaseLock(db); // Lock off.

            command.Dispose();
        }
    }
}
