// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;

using ComicReader.Common.KVStorage;

namespace ComicReader.Common.DebugTools;

internal static class DebugUtils
{
#if DEBUG
    private const bool IS_DEBUG_BUILD = true;
#else
    private const bool IS_DEBUG_BUILD = false;
#endif
    private const string KEY_DEBUG_MODE = "debug_mode";

    private static bool? _debugMode = null;

    public static bool DebugBuild => IS_DEBUG_BUILD;

    public static bool DebugMode
    {
        get
        {
            if (!_debugMode.HasValue)
            {
                _debugMode = KVDatabase.GetDefaultMethod().GetBoolean(GlobalConstants.KV_DB_APP, KEY_DEBUG_MODE, IS_DEBUG_BUILD);
            }
            return _debugMode.Value;
        }
        set
        {
            _debugMode = value;
            KVDatabase.GetDefaultMethod().SetBoolean(GlobalConstants.KV_DB_APP, KEY_DEBUG_MODE, value);
        }
    }

    public static bool DebugModeStrict => IS_DEBUG_BUILD && DebugMode;

    public static void Assert(bool condition)
    {
        if (!condition && DebugMode)
        {
            if (DebugBuild)
            {
                System.Diagnostics.Debugger.Break();
            }
            throw new AssertException();
        }
    }

    private class AssertException : Exception
    {
        public AssertException() { }

        public AssertException(string message) : base(message) { }

        public AssertException(string message, Exception inner) : base(message, inner) { }
    }
}
