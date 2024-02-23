using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Windows.Storage;

namespace ComicReader.Database
{
    public class SqlKey
    {
        public SqlKey(string name, object value = null)
        {
            Name = name;
            Value = value;
        }

        public string Name;
        public object Value;
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
            var connection = new SqliteConnection($"Filename={DatabasePath}");
            connection.Open();
            m_connection = connection;

            // Create tables.
            using (SqliteCommand command = NewCommand())
            {
                // Create comic table.
                command.CommandText = "CREATE TABLE IF NOT EXISTS " + ComicTable + " (" +
                    ComicData.Field.Id + " INTEGER PRIMARY KEY AUTOINCREMENT," + // 0
                    ComicData.Field.Type + " INTEGER NOT NULL," + // 1
                    ComicData.Field.Location + " TEXT NOT NULL," + // 2
                    ComicData.Field.Title1 + " TEXT," + // 3
                    ComicData.Field.Title2 + " TEXT," + // 4
                    ComicData.Field.Hidden + " BOOLEAN NOT NULL," + // 5
                    ComicData.Field.Rating + " INTEGER NOT NULL," + // 6
                    ComicData.Field.Progress + " INTEGER NOT NULL," + // 7
                    ComicData.Field.LastVisit + " TIMESTAMP NOT NULL," + // 8
                    ComicData.Field.LastPosition + " REAL NOT NULL," + // 9
                    ComicData.Field.ImageAspectRatios + " BLOB," + // 10
                    ComicData.Field.CoverFileCache + " TEXT)"; // 11
                await command.ExecuteNonQueryAsync();

                // Create tag category table.
                command.CommandText = "CREATE TABLE IF NOT EXISTS " + TagCategoryTable + " (" +
                    ComicData.Field.TagCategory.Id + " INTEGER PRIMARY KEY AUTOINCREMENT," + // 0
                    ComicData.Field.TagCategory.Name + " TEXT," + // 1
                    ComicData.Field.TagCategory.ComicId + " INTEGER REFERENCES " + ComicTable + "(" + ComicData.Field.Id + ") ON DELETE CASCADE)"; // 2
                await command.ExecuteNonQueryAsync();

                // Create tag table.
                command.CommandText = "CREATE TABLE IF NOT EXISTS " + TagTable + " (" +
                    ComicData.Field.Tag.Content + " TEXT," + // 0
                    ComicData.Field.Tag.ComicId + " INTEGER NOT NULL," + // 1
                    ComicData.Field.Tag.TagCategoryId + " INTEGER REFERENCES " + TagCategoryTable + "(" + ComicData.Field.TagCategory.Id + ") ON DELETE CASCADE)"; // 2
                await command.ExecuteNonQueryAsync();
            }
        }

        public static SqliteCommand NewCommand()
        {
            System.Diagnostics.Debug.Assert(m_connection != null);
            return m_connection.CreateCommand();
        }

        public static SqliteTransaction NewTransaction()
        {
            System.Diagnostics.Debug.Assert(m_connection != null);
            return m_connection.BeginTransaction();
        }

        public static async Task<long> Insert(string table, List<SqlKey> keys)
        {
            var field_names = new List<string>();
            var field_vals = new List<string>();
            var blobs = new List<KeyValuePair<string, MemoryStream>>();
            long rowid;

            using (SqliteCommand command = NewCommand())
            {
                foreach (SqlKey key in keys)
                {
                    field_names.Add(key.Name);
                    string param = "@" + key.Name;
                    field_vals.Add(param);
                    command.Parameters.AddWithValue(param, key.Value);
                }

                command.CommandText = "INSERT INTO " + table + " (" +
                    string.Join(',', field_names) + ") VALUES (" +
                    string.Join(',', field_vals) + ");" +
                    "SELECT LAST_INSERT_ROWID();";

                rowid = (long)command.ExecuteScalar();
            }

            // Copy to blobs.
            foreach (KeyValuePair<string, MemoryStream> pairs in blobs)
            {
                MemoryStream input_stream = pairs.Value;
                input_stream.Seek(0, SeekOrigin.Begin);

                using (var write_stream = new SqliteBlob(
                    m_connection, table, pairs.Key, rowid))
                {
                    await input_stream.CopyToAsync(write_stream);
                }
            }

            return rowid;
        }

        public static async Task<bool> Update(string table, SqlKey primary_key, List<SqlKey> keys)
        {
            if (keys.Count == 0)
            {
                throw new ArgumentException();
            }

            // Generate the command.
            SqliteCommand command = NewCommand();
            _ = command.Parameters.AddWithValue("@" + primary_key.Name, primary_key.Value);

            var fields = new List<string>();
            var blobs = new List<KeyValuePair<string, MemoryStream>>();

            foreach (SqlKey key in keys)
            {
                string param = "@" + key.Name;
                fields.Add(key.Name + "=" + param);
                command.Parameters.AddWithValue(param, key.Value);
            }

            string condition = " WHERE " + primary_key.Name + "=@" + primary_key.Name;
            command.CommandText = "UPDATE " + table + " SET " +
                string.Join(',', fields) + condition;

            // Execute the command.
            try
            {
                int rows_updated = await command.ExecuteNonQueryAsync();

                if (rows_updated == 0)
                {
                    return false;
                }

                // Copy to blobs.
                if (blobs.Count > 0)
                {
                    command.CommandText = "SELECT rowid FROM " + table + condition + " LIMIT 1";
                    long rowid = (long)command.ExecuteScalar();

                    foreach (KeyValuePair<string, MemoryStream> pairs in blobs)
                    {
                        MemoryStream input_stream = pairs.Value;
                        input_stream.Seek(0, SeekOrigin.Begin);

                        using (var write_stream = new SqliteBlob(
                            m_connection, table, pairs.Key, rowid))
                        {
                            await input_stream.CopyToAsync(write_stream);
                        }
                    }
                }

                return true;
            }
            finally
            {
                command.Dispose();
            }
        }

        public static async Task<bool> IsTableExist(string table)
        {
            SqliteCommand command = NewCommand();
            command.CommandText = "select count(*) from sqlite_master where type='table' and name='$table'";
            command.Parameters.AddWithValue("$table", table);
            long count = (long)await command.ExecuteScalarAsync();
            return count > 0;
        }

        // For backward compability.
        public static async Task<bool> IsDatabaseExist()
        {
            IStorageItem item = await DatabaseFolder.TryGetItemAsync(DatabaseFileName);
            return item != null;
        }
    }
}
