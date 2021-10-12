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

        public static SubFilter ParseFilter(string desc)
        {
            string[] pieces = desc.Split(':', 2);
            string filter_type = pieces[0].Trim().ToLower();

            string args = "";
            if (pieces.Length >= 2)
            {
                args = pieces[1].Trim();
            }

            return ParseFilter(filter_type, args);
        }

        public bool AddFilter(string desc)
        {
            SubFilter subfilter = ParseFilter(desc);
            if (subfilter == null)
            {
                return false;
            }
            return AddFilter(subfilter);
        }

        public bool AddFilter(SubFilter subfilter)
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

        public void RemoveFilter(string desc)
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

        public bool ContainFilter(string desc)
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
            Filter filter = new Filter();
            List<SubFilter> filters = ParseFilters(desc, out remaining);

            foreach (SubFilter f in filters)
            {
                filter.AddFilter(f);
            }

            return filter;
        }

        private static List<SubFilter> ParseFilters(string desc, out List<string> remaining)
        {
            remaining = new List<string>();
            List<SubFilter> filters = new List<SubFilter>();
            int bracket_cnt = 0;
            int i_start = 0;

            for (int i = 0; i < desc.Length; ++i)
            {
                char c = desc[i];

                if (c == '>')
                {
                    if (bracket_cnt == 1)
                    {
                        string sub_desc = desc.Substring(i_start, i - i_start);
                        i_start = i + 1;
                        SubFilter filter = ParseFilter(sub_desc);
                        if (filter != null) filters.Add(filter);
                    }
                    bracket_cnt--;
                }
                else if (c == '<')
                {
                    if (bracket_cnt == 0)
                    {
                        if (i != i_start)
                        {
                            remaining.Add(desc.Substring(i_start, i - i_start));
                        }
                        i_start = i + 1;
                    }
                    bracket_cnt++;
                }
            }

            if (desc.Length != i_start)
            {
                remaining.Add(desc.Substring(i_start, desc.Length - i_start));
            }

            return filters;
        }

        private static SubFilter ParseFilter(string filter_type, string args)
        {
            if (filter_type.Length >= 1 && filter_type[0] == '~')
            {
                SubFilter subfilter = ParseFilter(filter_type.Substring(1), args);

                if (subfilter == null)
                {
                    return null;
                }

                return new SubFilterInverse(subfilter);
            }

            if (filter_type == "or")
            {
                List<SubFilter> filters = ParseFilters(args, out List<string> _);
                if (filters.Count == 0)
                {
                    return null;
                }
                if (filters.Count == 1)
                {
                    return filters[0];
                }

                return new SubFilterOr(filters);
            }

            {
                string[] sub_args = args.Split(',');
                string unique_string = filter_type + ":";
                List<SubFilter> filters = new List<SubFilter>();

                foreach (string arg in sub_args)
                {
                    string true_arg = arg.Trim();
                    if (true_arg.Length == 0) continue;
                    SubFilter filter = ParseFilterRaw(filter_type, true_arg);
                    if (filter == null) continue;
                    filters.Add(filter);
                    unique_string += " " + true_arg;
                }

                if (filters.Count == 0)
                {
                    return null;
                }

                if (filters.Count == 1)
                {
                    return filters[0];
                }

                SubFilterOr res = new SubFilterOr(filters);
                res.SetUniqueString(unique_string);
                return res;
            }
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
        public abstract string UniqueString { get; }
        public abstract bool Pass(ComicData comic);
    }

    // <~...>
    public class SubFilterInverse : SubFilter
    {
        private SubFilter m_subfilter;

        public override string UniqueString => "~" + m_subfilter.UniqueString;

        public SubFilterInverse(SubFilter subfilter)
        {
            m_subfilter = subfilter;
        }

        public override bool Pass(ComicData comic)
        {
            return !m_subfilter.Pass(comic);
        }
    }

    // <or>
    public class SubFilterOr : SubFilter
    {
        private List<SubFilter> m_filters = new List<SubFilter>();
        private string m_unique_string = "";

        public override string UniqueString => m_unique_string;

        public SubFilterOr(IEnumerable<SubFilter> filters)
        {
            foreach (SubFilter filter in filters)
            {
                m_filters.Add(filter);
            }

            if (m_filters.Count == 0)
            {
                return;
            }

            m_unique_string = "or:";
            foreach (SubFilter filter in m_filters)
            {
                m_unique_string += filter.UniqueString;
            }
        }

        public override bool Pass(ComicData comic)
        {
            foreach (SubFilter filter in m_filters)
            {
                if (filter.Pass(comic))
                {
                    return true;
                }
            }
            return false;
        }

        public void SetUniqueString(string val)
        {
            m_unique_string = val;
        }
    }

    // <all>
    public class SubFilterAll : SubFilter
    {
        public override string UniqueString => "all";

        public override bool Pass(ComicData comic)
        {
            return true;
        }
    }

    // <dir>
    public class SubFilterDirectory : SubFilter
    {
        private string m_directory;
        public string Directory => m_directory;

        public override string UniqueString => "dir:" + m_directory;

        public SubFilterDirectory(string directory)
        {
            m_directory = directory.ToLower();
        }

        public override bool Pass(ComicData comic)
        {
            string comic_dir = comic.Directory.ToLower();
            return comic_dir.Length >= m_directory.Length && comic_dir.Substring(0, m_directory.Length).Equals(m_directory);
        }
    }

    // <hidden>
    public class SubFilterHidden : SubFilter
    {
        public override string UniqueString => "hidden";

        public override bool Pass(ComicData comic)
        {
            return comic.Hidden;
        }
    }
}
