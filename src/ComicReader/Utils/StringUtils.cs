using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace ComicReader.Utils
{
    class StringUtils
    {
        public class FileNameComparer : IComparer<string>
        {
            public int Compare(string x, string y)
            {
                if (int.TryParse(x, NumberStyles.Integer, CultureInfo.InvariantCulture, out int xint) &&
                    int.TryParse(y, NumberStyles.Integer, CultureInfo.InvariantCulture, out int yint))
                {
                    return xint - yint;
                }

                return x.Length != y.Length ? x.Length - y.Length : string.CompareOrdinal(x, y);
            }
        }

        public class OrdinalComparer : IComparer<string>
        {
            public int Compare(string x, string y)
            {
                return string.CompareOrdinal(x, y);
            }
        }

        public static string Join(string seperator, IEnumerable<string> list)
        {
            if (list.Count() == 0)
            {
                return "";
            }

            string res = list.First();
            bool first = true;

            foreach (string s in list)
            {
                if (first)
                {
                    first = false;
                    continue;
                }

                res += seperator + s;
            }

            return res;
        }

        public static int QuickMatch(List<string> keywords, string str)
        {
            int total_similarity = 0;
            str = str.ToLower();

            foreach (string keyword in keywords)
            {
                int similarity = 0;
                int keyword_ptr = 0;

                foreach (char c in str)
                {
                    if (c == keyword[keyword_ptr])
                    {
                        ++keyword_ptr;
                        if (keyword_ptr >= keyword.Length)
                        {
                            ++similarity;
                            keyword_ptr = 0;
                        }
                    }
                    else
                    {
                        keyword_ptr = 0;
                    }
                }

                if (similarity == 0)
                {
                    return 0;
                }

                total_similarity += similarity;
            }

            return total_similarity;
        }

        public static string TokenFromPath(string path)
        {
            return path.ToLower().Replace('\\', '%');
        }

        public static string UniquePath(string path)
        {
            return path.ToLower();
        }

        /// <summary>
        /// Case sensitive. You might need to lower both strings before calling this function.
        /// </summary>
        public static bool PathContain(string base_path, string child_path)
        {
            if (base_path.Length > child_path.Length)
            {
                return false;
            }

            return child_path.Substring(0, base_path.Length).Equals(base_path);
        }

        public static string ItemNameFromPath(string path)
        {
            int i = path.LastIndexOf('\\');

            if (i == -1)
            {
                return path;
            }

            return path.Substring(i + 1);
        }

        public static string ParentLocationFromLocation(string location)
        {
            int i = location.LastIndexOf('\\');

            if (i == -1)
            {
                return "";
            }

            if (i > 0 && location[i - 1] == '\\')
            {
                i--;
            }

            return location.Substring(0, i);

        }

        public static string ExtensionFromFilename(string filename)
        {
            int i = filename.LastIndexOf('.');

            if (i == -1)
            {
                return "";
            }

            return filename.Substring(i);
        }

        public static string DisplayNameFromFilename(string filename)
        {
            int i = filename.LastIndexOf('.');

            if (i == -1)
            {
                return filename;
            }

            return filename.Substring(0, i);
        }

        public static string ToPathNoTail(string path)
        {
            if (path.Length == 0)
            {
                return path;
            }

            if (path[path.Length - 1] == '\\')
            {
                return path.Substring(0, path.Length - 1);
            }

            return path;
        }

        public static string RandomFileName(int length)
        {
            const string symbols = "0123456789abcdefghijklmnopqrstuvwxyz";
            Random random = new Random();
            string res = "";

            for (int i = 0; i < length; ++i)
            {
                res += symbols[random.Next(symbols.Length)];
            }

            return res;
        }
    }
}