// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

using ComicReader.Common.DebugTools;

namespace ComicReader.Helpers.Navigation;

public class Route
{
    private static readonly Regex sSchemeRegex = new("^[a-z0-9]+$", RegexOptions.IgnoreCase);
    private static readonly Regex sHostRegex = new("^[_a-z0-9\\.]+$", RegexOptions.IgnoreCase);
    private static readonly Regex sPortRegex = new("^[0-9]*$", RegexOptions.None);
    private static readonly Regex sPathRegex = new("^[a-z0-9_/]*$", RegexOptions.IgnoreCase);
    private static readonly Regex sQueryKeyRegex = new("^[a-z0-9_]+$", RegexOptions.IgnoreCase);
    private static readonly Regex sQueryValueRegex = new("^[a-z0-9%_]+$", RegexOptions.IgnoreCase);
    private static readonly Regex sFragmentRegex = new("^[a-z0-9]*$", RegexOptions.IgnoreCase);

    public string Scheme { get; }
    public string Host { get; }
    public int Port { get; }
    public Dictionary<string, string> Queries { get; }
    public string Path { get; }
    public string Fragment { get; }
    public string Url { get => EvaluateUrl(); }

    public static Route Create(string url)
    {
        // parse scheme
        int index = url.IndexOf("://");
        if (index == -1)
        {
            ThrowParseException(0, url);
        }

        string scheme = url.Substring(0, index);
        string rest = url.Substring(index + 3);

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
            ThrowParseException(1, url);
        }

        if (!sHostRegex.Match(host).Success)
        {
            ThrowParseException(2, url);
        }

        if (!sPortRegex.Match(port).Success)
        {
            ThrowParseException(3, url);
        }

        int port_num;
        if (port.Length > 0)
        {
            if (!int.TryParse(port, out port_num))
            {
                ThrowParseException(4, url);
            }
        }
        else
        {
            port_num = -1;
        }

        if (!sPathRegex.Match(path).Success)
        {
            ThrowParseException(5, url);
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
                ThrowParseException(6, url);
            }

            if (!sQueryValueRegex.Match(value).Success)
            {
                ThrowParseException(7, url);
            }

            queries_dict[key] = Uri.UnescapeDataString(value);
        }

        if (!sFragmentRegex.Match(fragment).Success)
        {
            ThrowParseException(8, url);
        }

        return new Route(scheme, host, port_num, path, queries_dict, fragment);
    }

    private string _url;

    private Route(string scheme, string host, int port, string path, Dictionary<string, string> queries, string fragment)
    {
        Scheme = scheme;
        Host = host;
        Port = port;
        Path = path;
        Queries = queries;
        Fragment = fragment;
    }

    public Route WithParam(string key, string value)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            Logger.AssertNotReachHere("0D4C54A375DE2353");
            return this;
        }

        if (value == null)
        {
            if (Queries.Remove(key))
            {
                _url = null;
            }
        }
        else
        {
            Queries[key] = value;
            _url = null;
        }

        return this;
    }

    private string EvaluateUrl()
    {
        if (_url != null)
        {
            return _url;
        }
        _url = BuildUrl();
        return _url;
    }

    private string BuildUrl()
    {
        var urlBuilder = new StringBuilder();
        urlBuilder.Append(Scheme);
        urlBuilder.Append("://");
        urlBuilder.Append(Host);
        if (Port >= 0)
        {
            urlBuilder.Append(':');
            urlBuilder.Append(Port);
        }

        urlBuilder.Append(Uri.EscapeDataString(Path));
        if (Queries.Count > 0)
        {
            urlBuilder.Append('?');
            bool isFirst = true;
            foreach (KeyValuePair<string, string> query in Queries)
            {
                if (!isFirst)
                {
                    urlBuilder.Append('&');
                }
                isFirst = false;

                urlBuilder.Append(Uri.EscapeDataString(query.Key));
                urlBuilder.Append('=');
                urlBuilder.Append(Uri.EscapeDataString(query.Value));
            }
        }

        if (Fragment.Length > 0)
        {
            urlBuilder.Append('#');
            urlBuilder.Append(Fragment);
        }

        return urlBuilder.ToString();
    }

    private static string ThrowParseException(int code, string url)
    {
        throw new ParseException($"Parse URL error (code: {code}, url: {url})");
    }

    public class ParseException : Exception
    {
        public ParseException(string message) : base(message) { }
    }
}
