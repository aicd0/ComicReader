// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;

namespace ComicReader.Common.PageBase;

internal class PageBundle
{
    private readonly Dictionary<string, string> _parameters;

    public PageBundle(Dictionary<string, string> parameters)
    {
        _parameters = parameters;
    }

    public string GetString(string key, string defaultValue = "")
    {
        if (_parameters.TryGetValue(key, out string value))
        {
            return value;
        }

        return defaultValue;
    }

    public long GetLong(string key, long defaultValue = 0L)
    {
        if (_parameters.TryGetValue(key, out string value))
        {
            if (long.TryParse(value, out long longValue))
            {
                return longValue;
            }
        }

        return defaultValue;
    }
}
