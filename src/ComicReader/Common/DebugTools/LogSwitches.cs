// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;

namespace ComicReader.Common.DebugTools;

internal static class LogSwitches
{
    private static readonly bool ENABLE_LOG_FILTER = false;
    private static readonly bool ENABLE_LEVEL_BLACKLIST = false;
    private static readonly bool ENABLE_TAG_BLACKLIST = true;
    private static readonly bool ENABLE_TAG_WHITELIST = true;

    private static readonly Dictionary<int, bool> LEVEL_BLACKLIST = new()
    {
        { 0, true },
        { 1, true },
        { 2, true },
        { 3, true },
        { 4, true },
        { 5, true },
    };

    private static readonly Dictionary<LogTag, bool> TAG_BLACKLIST = new()
    {
        { LogTag.N("Logger"), false },
        { LogTag.N("TaskQueue"), false },
    };

    private static readonly Dictionary<LogTag, bool> TAG_WHITELIST = new()
    {
        { LogTag.N("Logger"), false },
        { LogTag.N("ReaderVertical", "Jump"), true },
        { LogTag.N("ReaderVertical", "ViewChanged"), true },
    };

    public static bool CanLog(int level, LogTag tag)
    {
        if (!ENABLE_LOG_FILTER)
        {
            return true;
        }

        if (ENABLE_LEVEL_BLACKLIST && !LEVEL_BLACKLIST.GetValueOrDefault(level, true))
        {
            return false;
        }

        if (ENABLE_TAG_BLACKLIST)
        {
            foreach (KeyValuePair<LogTag, bool> pair in TAG_BLACKLIST)
            {
                if (pair.Value)
                {
                    continue;
                }

                if (pair.Key.ContainsAny(tag))
                {
                    return false;
                }
            }
        }

        if (ENABLE_TAG_WHITELIST)
        {
            bool matched = false;
            foreach (KeyValuePair<LogTag, bool> pair in TAG_WHITELIST)
            {
                if (!pair.Value)
                {
                    continue;
                }

                if (pair.Key.ContainsAny(tag))
                {
                    matched = true;
                    break;
                }
            }
            if (!matched)
            {
                return false;
            }
        }

        return true;
    }
}
