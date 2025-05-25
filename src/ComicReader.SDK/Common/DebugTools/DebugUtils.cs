// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

namespace ComicReader.SDK.Common.DebugTools;

public static class DebugUtils
{
#if DEBUG
    private const bool IS_DEBUG_BUILD = true;
#else
    private const bool IS_DEBUG_BUILD = false;
#endif

    public static bool DebugBuild => IS_DEBUG_BUILD;

    public static bool DebugMode { get; set; } = false;

    public static bool DebugModeStrict => IS_DEBUG_BUILD && DebugMode;
}
