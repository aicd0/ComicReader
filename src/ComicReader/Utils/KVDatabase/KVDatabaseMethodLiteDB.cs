﻿using System.IO;
using LiteDB;
using Windows.Storage;

namespace ComicReader.Utils.KVDatabase
{
    internal class KVDatabaseMethodLiteDB : KVDatabaseMethod
    {
        private const string DATABASE_FILE_NAME = "kv_database_litedb.db";

        private static StorageFolder DatabaseFolder => ApplicationData.Current.LocalFolder;
        private static string DatabasePath => Path.Combine(DatabaseFolder.Path, DATABASE_FILE_NAME);

        private KVDatabaseMethodLiteDB() { }

        public override void SetString(string lib, string key, string value)
        {
            SetValue(lib, key, value);
        }

        public override string GetString(string lib, string key)
        {
            return GetValue(lib, key);
        }

        public override void SetBoolean(string lib, string key, bool value)
        {
            string s = value ? "true" : "false";
            SetValue(lib, key, s);
        }

        public override bool? GetBoolean(string lib, string key)
        {
            string s = GetValue(lib, key);
            if (s == null)
            {
                return null;
            }
            return s.Equals("true");
        }

        private string GetValue(string lib, string key)
        {
            using(LiteDatabase db = new LiteDatabase(DatabasePath))
            {
                ILiteCollection<KVPair> col = db.GetCollection<KVPair>(lib);
                col.EnsureIndex(x => x.Key);
                KVPair pair = col.FindOne(x => x.Key == key);
                if (pair == null)
                {
                    return null;
                }
                return pair.Value;
            }
        }

        private void SetValue(string lib, string key, string value)
        {
            using (LiteDatabase db = new LiteDatabase(DatabasePath))
            {
                ILiteCollection<KVPair> col = db.GetCollection<KVPair>(lib);
                col.EnsureIndex(x => x.Key);
                KVPair pair = col.FindOne(x => x.Key == key);
                if (pair == null)
                {
                    pair = new KVPair
                    {
                        Key = key,
                        Value = value
                    };
                    col.Insert(pair);
                }
                else
                {
                    if (pair.Value == value)
                    {
                        return;
                    }
                    pair.Value = value;
                    col.Update(pair);
                }
            }
        }

        private static KVDatabaseMethodLiteDB mInstance;

        public static KVDatabaseMethodLiteDB GetInstance()
        {
            if (mInstance == null)
            {
                mInstance = new KVDatabaseMethodLiteDB();
            }
            return mInstance;
        }

        private class KVPair
        {
            public string Key { get; set; }
            public string Value { get; set; }
        }
    }
}