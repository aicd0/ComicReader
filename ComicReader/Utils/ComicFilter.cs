using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ComicReader.Data;

namespace ComicReader.Utils.Search
{
    public class Filter
    {
        private List<SubFilter> m_subfilters = new List<SubFilter>();

        public string DescriptionBrief
        {
            get
            {
                List<SubFilter> cpy = new List<SubFilter>(m_subfilters);
                _ = cpy.RemoveAll(x => x is SubFilterAll);

                if (cpy.Count == 0)
                {
                    return "All in your library";
                }

                if (cpy.Count == 1)
                {
                    if (cpy[0] is SubFilterHidden)
                    {
                        return "All hidden items";
                    }
                }
                else if (cpy.Count == 2)
                {
                    if (cpy[0] is SubFilterDirectory && cpy[1].ToString() == "~hidden")
                    {
                        return "All items in " + (cpy[0] as SubFilterDirectory).Directory;
                    }
                }

                return "";
            }
        }

        public string DescriptionDetailed
        {
            get
            {
                List<SubFilter> cpy = new List<SubFilter>(m_subfilters);
                _ = cpy.RemoveAll(x => x is SubFilterAll);

                bool is_multiple = cpy.Count > 1;
                string res = cpy.Count.ToString() + " " + (is_multiple ? "filters" : "filter") + " applied:";
                foreach (SubFilter f in cpy)
                {
                    res += " <" + f.ToString() + ">";
                }
                return res;
            }
        }

        public bool AddSubFilter(string desc)
        {
            string[] pieces = desc.Split(':', 2);
            string filter_type = pieces[0].Trim().ToLower();

            string args = "";
            if (pieces.Length >= 2)
            {
                args = pieces[1].Trim();
            }

            SubFilter subfilter = ParseFilter(filter_type, args);
            if (subfilter == null)
            {
                return false;
            }
            return AddSubFilter(subfilter);
        }

        public bool AddSubFilter(SubFilter subfilter)
        {
            string id = subfilter.ToString();

            foreach (SubFilter f in m_subfilters)
            {
                if (f.ToString().Equals(id))
                {
                    return false;
                }
            }

            m_subfilters.Add(subfilter);
            return true;
        }

        public void RemoveSubFilter(string desc)
        {
            for (int i = 0; i < m_subfilters.Count; ++i)
            {
                if (m_subfilters[i].ToString().Equals(desc))
                {
                    m_subfilters.RemoveAt(i);
                    --i;
                }
            }
        }

        public bool ContainSubFilter(string desc)
        {
            foreach (SubFilter f in m_subfilters)
            {
                if (f.ToString().Equals(desc))
                {
                    return true;
                }
            }
            return false;
        }

        public bool Pass(ComicData comic)
        {
            foreach (SubFilter subfilter in m_subfilters)
            {
                if (!subfilter.Pass(comic))
                {
                    return false;
                }
            }
            return true;
        }

        public bool IsEmpty()
        {
            return m_subfilters.Count == 0;
        }

        public static Filter Parse(string desc, out List<string> remaining)
        {
            remaining = new List<string>();
            Filter filter = new Filter();
            bool filter_started = false;
            int i_start = 0;

            for (int i = 0; i < desc.Length; ++i)
            {
                char c = desc[i];

                if (filter_started)
                {
                    if (c == '>')
                    {
                        filter_started = false;
                        string sub_desc = desc.Substring(i_start, i - i_start);
                        i_start = i + 1;
                        _ = filter.AddSubFilter(sub_desc);
                    }
                }
                else
                {
                    if (c == '<')
                    {
                        if (i != i_start)
                        {
                            remaining.Add(desc.Substring(i_start, i - i_start));
                        }
                        filter_started = true;
                        i_start = i + 1;
                    }
                }
            }

            if (desc.Length != i_start)
            {
                remaining.Add(desc.Substring(i_start, desc.Length - i_start));
            }

            return filter;
        }

        private static SubFilter ParseFilter(string filter_type, string args)
        {
            if (filter_type.Length >= 1 && filter_type[0] == '~')
            {
                SubFilter subfilter = ParseFilterRaw(filter_type.Substring(1), args);
                if (subfilter == null)
                {
                    return null;
                }
                return new FilterInverse(subfilter);
            }
            return ParseFilterRaw(filter_type, args);
        }

        private static SubFilter ParseFilterRaw(string filter_type, string args)
        {
            switch (filter_type)
            {
                case "all":
                    return new SubFilterAll();
                case "dir":
                case "directory":
                    return new SubFilterDirectory(args);
                case "hidden":
                    return new SubFilterHidden();
                default:
                    return null;
            }
        }
    }

    // subfilter definitions
    public abstract class SubFilter
    {
        public abstract bool Pass(ComicData comic);
    }

    // <~...>
    public class FilterInverse : SubFilter
    {
        private SubFilter m_subfilter;

        public FilterInverse(SubFilter subfilter)
        {
            m_subfilter = subfilter;
        }

        public override bool Pass(ComicData comic)
        {
            return !m_subfilter.Pass(comic);
        }

        public override string ToString()
        {
            return "~" + m_subfilter.ToString();
        }
    }

    // <all>
    public class SubFilterAll : SubFilter
    {
        public override bool Pass(ComicData comic)
        {
            return true;
        }

        public override string ToString()
        {
            return "all";
        }
    }

    // <dir>
    public class SubFilterDirectory : SubFilter
    {
        private string m_directory;
        public string Directory => m_directory;

        public SubFilterDirectory(string directory)
        {
            m_directory = directory.ToLower();
        }

        public override bool Pass(ComicData comic)
        {
            string comic_dir = comic.Directory.ToLower();
            return comic_dir.Length >= m_directory.Length && comic_dir.Substring(0, m_directory.Length).Equals(m_directory);
        }

        public override string ToString()
        {
            return "dir:" + m_directory;
        }
    }

    // <hidden>
    public class SubFilterHidden : SubFilter
    {
        public override bool Pass(ComicData comic)
        {
            return comic.Hidden;
        }

        public override string ToString()
        {
            return "hidden";
        }
    }
}
