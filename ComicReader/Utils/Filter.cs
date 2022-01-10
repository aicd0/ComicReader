using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ComicReader.Database;

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
                return Utils.C0.TryGetResourceString("AllComics");
            }
            else if (unique_string == "<hidden>")
            {
                return Utils.C0.TryGetResourceString("AllHidden");
            }
            else if (m_subfilters.Count == 2 && m_subfilters[0] is SubFilterDirectory && m_subfilters[1].UniqueString == "~hidden")
            {
                string dir = (m_subfilters[0] as SubFilterDirectory).Directory;
                string format_string = Utils.C0.TryGetResourceString("AllComicsIn");
                format_string = format_string.Replace("$path", dir);
                return format_string;
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

            string format_string = Utils.C0.TryGetResourceString("FilteredBy");
            format_string = format_string.Replace("$count", cpy.Count.ToString());
            string res = format_string + ": ";

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

        public string ToSQL(SqliteParameterCollection parameters)
        {
            SqlParameterContext ctx = new SqlParameterContext(parameters);

            string sql = "1";

            foreach (SubFilter sub in m_subfilters)
            {
                string sub_sql = sub.ToSQL(ctx);

                if (sub_sql == null)
                {
                    continue;
                }

                sql += " AND (" + sub_sql + ")";
            }

            return sql;
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
            int bracket_start = 0;
            int keyword_start = 0;

            for (int i = 0; i < desc.Length; ++i)
            {
                char c = desc[i];

                if (c == '>')
                {
                    bracket_cnt--;

                    if (bracket_cnt == 0)
                    {
                        string sub_desc = desc.Substring(bracket_start + 1, i - bracket_start - 1);
                        SubFilter filter = ParseFilter(sub_desc);

                        if (filter != null)
                        {
                            filters.Add(filter);

                            if (bracket_start != keyword_start)
                            {
                                remaining.Add(desc.Substring(keyword_start, bracket_start - keyword_start));
                            }

                            keyword_start = i + 1;
                        }
                    }

                    if (bracket_cnt < 0)
                    {
                        bracket_cnt = 0;
                    }
                }
                else if (c == '<')
                {
                    if (bracket_cnt == 0)
                    {
                        bracket_start = i;
                    }

                    bracket_cnt++;
                }
            }

            if (desc.Length != keyword_start)
            {
                remaining.Add(desc.Substring(keyword_start, desc.Length - keyword_start));
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
                case "folder":
                case "location":
                case "path":
                    return new SubFilterDirectory(args);
                case "hidden":
                case "hide":
                    return new SubFilterHidden();
                case "id":
                case "no":
                case "num":
                case "number":
                    return new SubFilterId(args);
                case "rating":
                case "ratings":
                case "rate":
                case "star":
                case "stars":
                    return new SubFilterRating(args);
                case "tag":
                case "tags":
                    return new SubFilterTag(args);
                default:
                    return new SubFilterCategoryTag(filter_type, args);
            }
        }
    }

    public class SqlParameterContext
    {
        private SqliteParameterCollection m_params;
        private int m_index = 0;

        public SqlParameterContext(SqliteParameterCollection parameters)
        {
            m_params = parameters;
        }

        public string AddValue(object value)
        {
            string name = "$" + m_index.ToString();
            m_params.AddWithValue(name, value);
            m_index++;
            return name;
        }
    }

    // subfilter definitions
    public abstract class SubFilter
    {
        public abstract string UniqueString { get; }

        public virtual string ToSQL(SqlParameterContext ctx)
        {
            return null; // Not supported.
        }

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

        public override string ToSQL(SqlParameterContext ctx)
        {
            string sub_sql = m_subfilter.ToSQL(ctx);

            if (sub_sql == null)
            {
                return null;
            }

            return "NOT (" + sub_sql + ")";
        }

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

        public override string ToSQL(SqlParameterContext ctx)
        {
            string sql = "";

            for (int i = 0; i < m_filters.Count; i++)
            {
                string sub_sql = m_filters[i].ToSQL(ctx);

                if (sub_sql == null)
                {
                    return null;
                }

                if (i > 0)
                {
                    sql += " OR ";
                }

                sql += "(" + sub_sql + ")";
            }

            return sql;
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

        public override string ToSQL(SqlParameterContext ctx)
        {
            string sql = "";

            for (int i = 0; i < m_filters.Count; i++)
            {
                string sub_sql = m_filters[i].ToSQL(ctx);

                if (sub_sql == null)
                {
                    return null;
                }

                if (i > 0)
                {
                    sql += " AND ";
                }

                sql += "(" + sub_sql + ")";
            }

            return sql;
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

    // <...: ...>
    // Tags filter with category specified.
    public class SubFilterCategoryTag : SubFilter
    {
        private readonly string m_category;
        private readonly string m_tag;

        public override string UniqueString => m_category + ": " + m_tag;

        public SubFilterCategoryTag(string category, string tag)
        {
            m_category = category.ToLower();
            m_tag = tag.ToLower();
        }

        public override string ToSQL(SqlParameterContext ctx)
        {
            return ctx.AddValue(m_tag) + " COLLATE NOCASE IN (SELECT " + ComicData.Field.Tag.Content +
                " FROM " + SqliteDatabaseManager.TagTable + " WHERE " + ComicData.Field.Tag.TagCategoryId +
                " IN (SELECT " + ComicData.Field.TagCategory.Id + " FROM " + SqliteDatabaseManager.TagCategoryTable +
                " WHERE " + ComicData.Field.TagCategory.Name + "=" + ctx.AddValue(m_category) +
                " COLLATE NOCASE AND " + SqliteDatabaseManager.ComicTable + "." + ComicData.Field.Id + "=" +
                ComicData.Field.TagCategory.ComicId + "))";
        }

        public override bool Pass(ComicData comic)
        {
            foreach (TagData tag_data in comic.Tags)
            {
                if (!tag_data.Name.ToLower().Equals(m_category))
                {
                    continue;
                }

                foreach (string tag in tag_data.Tags)
                {
                    if (tag.ToLower().Equals(m_tag))
                    {
                        return true;
                    }
                }

                return false;
            }

            return false;
        }
    }

    // <all>
    public class SubFilterAll : SubFilter
    {
        public override string UniqueString => "all";

        public override string ToSQL(SqlParameterContext ctx)
        {
            return "1";
        }

        public override bool Pass(ComicData comic)
        {
            return true;
        }
    }

    // <dir>
    public class SubFilterDirectory : SubFilter
    {
        private readonly string m_directory;
        public string Directory => m_directory;

        public override string UniqueString => "dir: " + m_directory;

        public SubFilterDirectory(string directory)
        {
            m_directory = directory.ToLower().Replace('/', '\\');
        }

        public override string ToSQL(SqlParameterContext ctx)
        {
            return ComicData.Field.Directory + " LIKE " + ctx.AddValue(m_directory + "%");
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

        public override string ToSQL(SqlParameterContext ctx)
        {
            return ComicData.Field.Hidden + "=1";
        }

        public override bool Pass(ComicData comic)
        {
            return comic.Hidden;
        }
    }

    // <id>
    public class SubFilterId : SubFilter
    {
        private readonly long m_id;
        private readonly bool m_valid;

        public override string UniqueString => "id: " + m_id.ToString();

        public SubFilterId(string id)
        {
            if (id.Length > 0 && id[0] == '#')
            {
                id = id.Substring(1);
            }

            m_valid = long.TryParse(id, out m_id);
        }

        public override string ToSQL(SqlParameterContext ctx)
        {
            if (m_valid)
            {
                return ComicData.Field.Id + "=" + m_id.ToString();
            }
            else
            {
                return "0";
            }
        }

        public override bool Pass(ComicData comic)
        {
            return m_valid && comic.Id == m_id;
        }
    }

    // <rating>
    public class SubFilterRating : SubFilter
    {
        private readonly int m_rating;
        private readonly bool m_valid;

        public override string UniqueString => "rating: " + m_rating.ToString();

        public SubFilterRating(string rating)
        {
            m_valid = int.TryParse(rating, out m_rating);

            if (m_valid && m_rating < 1)
            {
                m_rating = -1;
            }
        }

        public override string ToSQL(SqlParameterContext ctx)
        {
            if (m_valid)
            {
                return ComicData.Field.Id + "=" + m_rating.ToString();
            }
            else
            {
                return "0";
            }
        }

        public override bool Pass(ComicData comic)
        {
            return m_valid && comic.Rating == m_rating;
        }
    }

    // <tag>
    public class SubFilterTag : SubFilter
    {
        private readonly string m_tag;

        public override string UniqueString => "tag: " + m_tag;

        public SubFilterTag(string tag)
        {
            m_tag = tag.ToLower();
        }

        public override string ToSQL(SqlParameterContext ctx)
        {
            return ctx.AddValue(m_tag) + " COLLATE NOCASE IN (SELECT " + ComicData.Field.Tag.Content +
                " FROM " + SqliteDatabaseManager.TagTable + " WHERE " + ComicData.Field.Tag.ComicId +
                "=" + SqliteDatabaseManager.ComicTable + "." + ComicData.Field.Id + ")";
        }

        public override bool Pass(ComicData comic)
        {
            foreach (TagData tag_data in comic.Tags)
            {
                foreach (string tag in tag_data.Tags)
                {
                    if (tag.ToLower().Equals(m_tag))
                    {
                        return true;
                    }
                }
            }

            return false;
        }
    }
}
