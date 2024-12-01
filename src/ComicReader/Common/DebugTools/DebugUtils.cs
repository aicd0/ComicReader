// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;

namespace ComicReader.Common.DebugTools;

internal static class DebugUtils
{
#if DEBUG
    private const bool IS_DEBUG_BUILD = true;
#else
    private const bool IS_DEBUG_BUILD = false;
#endif

    public static bool DebugMode => IS_DEBUG_BUILD && AppStatusPreserver.DebugMode;

    public static void Assert(bool condition)
    {
        if (!condition && DebugMode)
        {
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
