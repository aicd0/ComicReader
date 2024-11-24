// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

namespace ComicReader.Common.DebugTools;

internal static class DebugUtils
{
#if DEBUG
    public const bool IS_DEBUG_BUILD = true;
#else
    public const bool IS_DEBUG_BUILD = false;
#endif

    public static bool IsDebugEnabled => Database.XmlDatabase.Settings.DebugMode;
}
