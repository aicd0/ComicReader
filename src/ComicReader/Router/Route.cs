using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace ComicReader.Router;
internal class Route
{
    private static Regex sSchemeRegex = new Regex("^[a-z0-9]+$", RegexOptions.IgnoreCase);
    private static Regex sHostRegex = new Regex("^[a-z0-9\\.]+$", RegexOptions.IgnoreCase);
    private static Regex sPortRegex = new Regex("^[0-9]*$", RegexOptions.None);
    private static Regex sPathRegex = new Regex("^[a-z0-9_/]*$", RegexOptions.IgnoreCase);
    private static Regex sQueryKeyRegex = new Regex("^[a-z0-9_]+$", RegexOptions.IgnoreCase);
    private static Regex sQueryValueRegex = new Regex("^[a-z0-9%_]+$", RegexOptions.IgnoreCase);
    private static Regex sFragmentRegex = new Regex("^[a-z0-9]*$", RegexOptions.IgnoreCase);

    private readonly string _url;
    private readonly Dictionary<string, string> _params = new();

    public Route(string url)
    {
        _url = url;
    }

    public Route WithParam(string key, string value)
    {
        _params.Add(key, value);
        return this;
    }

    public NavigationBundle Process()
    {
        // parse scheme
        int index = _url.IndexOf("://");
        if (index == -1)
        {
            ThrowParseException(0);
        }

        string scheme = _url.Substring(0, index);
        string rest = _url.Substring(index + 3);

        // parse fragment
        index = rest.LastIndexOf("#");
        string fragment;
        if (index != -1)
        {
            fragment = rest.Substring(index + 1);
            rest = rest.Substring(0, index);
        }
        else
        {
            fragment = "";
        }

        // parse query
        index = rest.LastIndexOf("?");
        string query;
        if (index != -1)
        {
            query = rest.Substring(index + 1);
            rest = rest.Substring(0, index);
        }
        else
        {
            query = "";
        }

        // parse path
        index = rest.IndexOf("/");
        string path;
        if (index != -1)
        {
            path = rest.Substring(index);
            rest = rest.Substring(0, index);
        }
        else
        {
            path = "";
        }

        // parse port
        index = rest.LastIndexOf(":");
        string port;
        if (index != -1)
        {
            port = rest.Substring(index + 1);
            rest = rest.Substring(0, index);
        }
        else
        {
            port = "";
        }

        // parse host
        string host = rest;

        // check validity
        if (!sSchemeRegex.Match(scheme).Success)
        {
            ThrowParseException(1);
        }

        if (!sHostRegex.Match(host).Success)
        {
            ThrowParseException(2);
        }

        if (!sPortRegex.Match(port).Success)
        {
            ThrowParseException(3);
        }

        int port_num;
        if (port.Length > 0)
        {
            if (!int.TryParse(port, out port_num))
            {
                ThrowParseException(4);
            }
        }
        else
        {
            port_num = -1;
        }

        if (!sPathRegex.Match(path).Success)
        {
            ThrowParseException(5);
        }

        string[] queries = query.Split('&');
        var queries_dict = new Dictionary<string, string>();
        foreach (string q in queries)
        {
            if (q.Length == 0)
            {
                continue;
            }

            index = q.IndexOf("=");
            string key = q.Substring(0, index);
            string value = q.Substring(index + 1);
            if (!sQueryKeyRegex.Match(key).Success)
            {
                ThrowParseException(6);
            }

            if (!sQueryValueRegex.Match(value).Success)
            {
                ThrowParseException(7);
            }

            queries_dict[key] = Uri.UnescapeDataString(value);
        }

        if (!sFragmentRegex.Match(fragment).Success)
        {
            ThrowParseException(8);
        }

        // merge params
        foreach (KeyValuePair<string, string> entry in _params)
        {
            if (!sQueryKeyRegex.Match(entry.Key).Success)
            {
                ThrowParseException(9);
            }

            queries_dict[entry.Key] = entry.Value;
        }

        return Process(scheme, host, port_num, path, queries_dict, fragment);
    }

    private NavigationBundle Process(string scheme, string host, int port, string path, Dictionary<string, string> queries, string fragment)
    {
        var routeInfo = new RouteInfo(scheme, host, port, path, queries, fragment);
        return AppRouter.Process(routeInfo);
    }

    private string ThrowParseException(int code)
    {
        throw new ParseException($"Parse URL error (code: {code}, url: {_url})");
    }

    public class ParseException : Exception
    {
        public ParseException(string message) : base(message) { }
    }
}
