using System;
using System.Collections.Generic;
using System.Text;

namespace ComicReader.Router;
internal class RouteInfo
{
    public RouteInfo(string scheme, string host, int port, string path, Dictionary<string, string> queries, string fragment)
    {
        Scheme = scheme;
        Host = host;
        Port = port;
        Path = path;
        Queries = queries;
        Fragment = fragment;

        var urlBuilder = new StringBuilder();
        urlBuilder.Append(scheme);
        urlBuilder.Append("://");
        urlBuilder.Append(host);
        if (port >= 0)
        {
            urlBuilder.Append(':');
            urlBuilder.Append(port);
        }

        urlBuilder.Append(Uri.EscapeDataString(path));
        if (queries.Count > 0)
        {
            urlBuilder.Append('?');
            bool isFirst = true;
            foreach (KeyValuePair<string, string> query in queries)
            {
                if (!isFirst)
                {
                    urlBuilder.Append('&');
                }

                urlBuilder.Append(Uri.EscapeDataString(query.Key));
                urlBuilder.Append('=');
                urlBuilder.Append(Uri.EscapeDataString(query.Value));
            }
        }

        if (fragment.Length > 0)
        {
            urlBuilder.Append('#');
            urlBuilder.Append(fragment);
        }

        Url = urlBuilder.ToString();
    }

    public string Scheme { get; }
    public string Host { get; }
    public int Port { get; }
    public Dictionary<string, string> Queries { get; }
    public string Path { get; }
    public string Fragment { get; }
    public string Url { get; }
}
