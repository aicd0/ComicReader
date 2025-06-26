// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

namespace ComicReader.SDK.Common.DebugTools;

public static class SentryManager
{
    private static volatile bool _initialized = false;

    public static void Initialize(string dsn, IReadOnlyDictionary<string, string> tags)
    {
        if (_initialized)
        {
            return;
        }

        if (dsn.Length == 0)
        {
            return;
        }

        SentrySdk.Init(o =>
        {
            o.AutoSessionTracking = true;
            o.Dsn = dsn;

            foreach (KeyValuePair<string, string> tag in tags)
            {
                o.DefaultTags[tag.Key] = tag.Value;
            }
        });
        _initialized = true;

        SentrySdk.CaptureMessage("SentryInitialization");
    }

    public static void CaptureException(Exception exception)
    {
        if (!_initialized)
        {
            return;
        }
        SentrySdk.CaptureException(exception);
    }
}
