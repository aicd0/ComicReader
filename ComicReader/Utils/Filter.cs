﻿using System;
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

        public string DescriptionBrief()
        {
            string unique_string = "";

            foreach (SubFilter filter in m_subfilters)
            {
                unique_string += "<" + filter.UniqueString + ">";
            }

            if (unique_string == "<~hidden>")
            {
                return "All in your library";
            }
            else if (unique_string == "<hidden>")
            {
                return "All hidden items";
            }
            else if (m_subfilters.Count == 2 && m_subfilters[0] is SubFilterDirectory && m_subfilters[1].UniqueString == "~hidden")
            {
                return "All items in " + (m_subfilters[0] as SubFilterDirectory).Directory;
            }

            return "";
        }

        public string DescriptionDetailed()
        {
            List<SubFilter> cpy = new List<SubFilter>(m_subfilters);
            _ = cpy.RemoveAll(x => x.UniqueString == "~hidden");

            if (ContainsFilter("hidden", cpy))
            {
                cpy = m_subfilters;
            }

            if (cpy.Count == 0)
            {
                return "";
            }

            bool is_multiple = cpy.Count > 1;
            string res = cpy.Count.ToString() + " " + (is_multiple ? "filters" : "filter") + " applied:";

            foreach (SubFilter f in cpy)
            {
                res += " <" + f.UniqueString + ">";
            }

            return res;
        }

        public bool AddFilter(string desc)
        {
            SubFilter subfilter = ParseFilter(desc);

            if (subfilter == null)
            {
                return false;
            }

            AddFilter(subfilter);
            return true;
        }

        public void AddFilter(SubFilter subfilter)
        {
            m_subfilters.Add(subfilter);
            Optimize();
        }

        public void RemoveFilter(string desc)
        {
            for (int i = m_subfilters.Count - 1; i >= 0; --i)
            {
                if (m_subfilters[i].UniqueString.Equals(desc))
                {
                    m_subfilters.RemoveAt(i);
                }
            }

            Optimize();
        }

        public bool ContainsFilter(string desc)
        {
            return ContainsFilter(desc, m_subfilters);
        }

        private static bool ContainsFilter(string desc, List<SubFilter> subfilters)
        {
            foreach (SubFilter f in subfilters)
            {
                if (f.ContainsFilter(desc))
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

            filter.Optimize();
            return filter;
        }

        private void Optimize()
        {
            if (m_subfilters.Count == 0)
            {
                return;
            }

            _ = m_subfilters.RemoveAll(x => x is SubFilterAll);

            if (m_subfilters.Count == 0)
            {
                m_subfilters.Add(new SubFilterAll());
                return;
            }

            m_subfilters.OrderBy((SubFilter x) => x.UniqueString);
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

        private static SubFilter ParseFilter(string desc)
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

            if (filter_type == "or" || filter_type == "and")
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

                if (filter_type == "or") return new SubFilterOr(filters);
                else return new SubFilterAnd(filters);
            }

            {
                List<string> sub_args = args.Split(',').ToList();

                for (int i = sub_args.Count - 1; i >= 0; --i)
                {
                    sub_args[i] = sub_args[i].Trim();

                    if (sub_args[i].Length == 0)
                    {
                        sub_args.RemoveAt(i);
                    }
                }

                if (sub_args.Count == 0)
                {
                    sub_args.Add("");
                }

                List<SubFilter> filters = new List<SubFilter>();
                List<string> all_unique_strings = new List<string>();

                foreach (string arg in sub_args)
                {
                    SubFilter filter = ParseFilterRaw(filter_type, arg);

                    if (filter == null)
                    {
                        continue;
                    }

                    filters.Add(filter);
                    all_unique_strings.Add(arg);
                }

                if (filters.Count == 0)
                {
                    return null;
                }

                if (filters.Count == 1)
                {
                    return filters[0];
                }

                {
                    string unique_string = filter_type + ": " + string.Join(", ", all_unique_strings);
                    SubFilterOr filter = new SubFilterOr(filters);
                    filter.SetUniqueString(unique_string);
                    return filter;
                }
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

        public virtual bool ContainsFilter(string desc)
        {
            return desc == UniqueString;
        }
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

        public override bool ContainsFilter(string desc)
        {
            return base.ContainsFilter(desc) || m_subfilter.ContainsFilter(desc);
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

            List<string> all_unique_strings = new List<string>();

            foreach (SubFilter filter in m_filters)
            {
                all_unique_strings.Add("<" + filter.UniqueString + ">");
            }

            m_unique_string = "or: " + string.Join(", ", all_unique_strings);
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

        public override bool ContainsFilter(string desc)
        {
            foreach (SubFilter filter in m_filters)
            {
                if (filter.ContainsFilter(desc))
                {
                    return true;
                }
            }

            return base.ContainsFilter(desc);
        }

        public void SetUniqueString(string val)
        {
            m_unique_string = val;
        }
    }

    // <and>
    public class SubFilterAnd : SubFilter
    {
        private List<SubFilter> m_filters = new List<SubFilter>();
        private string m_unique_string = "";

        public override string UniqueString => m_unique_string;

        public SubFilterAnd(IEnumerable<SubFilter> filters)
        {
            foreach (SubFilter filter in filters)
            {
                m_filters.Add(filter);
            }

            if (m_filters.Count == 0)
            {
                return;
            }

            List<string> all_unique_strings = new List<string>();

            foreach (SubFilter filter in m_filters)
            {
                all_unique_strings.Add("<" + filter.UniqueString + ">");
            }

            m_unique_string = "and: " + string.Join(", ", all_unique_strings);
        }

        public override bool Pass(ComicData comic)
        {
            foreach (SubFilter filter in m_filters)
            {
                if (!filter.Pass(comic))
                {
                    return false;
                }
            }

            return true;
        }

        public override bool ContainsFilter(string desc)
        {
            foreach (SubFilter filter in m_filters)
            {
                if (filter.ContainsFilter(desc))
                {
                    return true;
                }
            }

            return base.ContainsFilter(desc);
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

        public override string UniqueString => "dir: " + m_directory;

        public SubFilterDirectory(string directory)
        {
            m_directory = directory.ToLower().Replace('/', '\\');
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
