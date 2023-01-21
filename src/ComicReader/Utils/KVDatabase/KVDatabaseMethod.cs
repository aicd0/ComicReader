using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ComicReader.Utils.KVDatabase
{
    abstract internal class KVDatabaseMethod
    {
        private Dictionary<string, KVDatabaseLib> mLibs = new Dictionary<string, KVDatabaseLib>();

        public KVDatabaseLib With(string libName)
        {
            if (mLibs.TryGetValue(libName, out KVDatabaseLib lib))
            {
                return lib;
            }
            lib = new KVDatabaseLib(this, libName);
            mLibs.Add(libName, lib);
            return lib;
        }

        public abstract void SetString(string lib, string key, string value);

        public abstract string GetString(string lib, string key);

        public string GetString(string lib, string key, string defaultValue)
        {
            string value = GetString(lib, key);
            if (value != null)
            {
                return value;
            }
            return defaultValue;
        }

        public abstract void SetBoolean(string lib, string key, bool value);

        public abstract bool? GetBoolean(string lib, string key);

        public bool GetBoolean(string lib, string key, bool defaultValue)
        {
            bool? value = GetBoolean(lib, key);
            if (value.HasValue)
            {
                return value.Value;
            }
            return defaultValue;
        }
    }
}
