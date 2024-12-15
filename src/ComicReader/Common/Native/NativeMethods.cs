// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Runtime.InteropServices;

using Microsoft.Win32.SafeHandles;

namespace ComicReader.Common.Native;

internal class NativeMethods
{
    // https://docs.microsoft.com/en-us/windows/win32/api/fileapifromapp/nf-fileapifromapp-createfilefromappw
    [DllImport("api-ms-win-core-file-fromapp-l1-1-0.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    internal static extern SafeFileHandle CreateFileFromApp(string lpFileName,
        int dwDesiredAccess,
        int dwShareMode,
        nint lpSecurityAttributes,
        uint dwCreationDisposition,
        uint dwFlagsAndAttributes,
        SafeFileHandle hTemplateFile
    );

    // https://docs.microsoft.com/en-us/windows/win32/api/fileapifromapp/nf-fileapifromapp-findfirstfileexfromappw
    [DllImport("api-ms-win-core-file-fromapp-l1-1-0.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    internal static extern nint FindFirstFileExFromApp(
        string lpFileName,
        NativeModels.FindExInfoLevel fInfoLevelId,
        out NativeModels.Win32FindData lpFindFileData,
        NativeModels.FIndexSearchOps fSearchOp,
        nint lpSearchFilter,
        int dwAdditionalFlags
    );

    [DllImport("api-ms-win-core-file-l1-1-0.dll", CharSet = CharSet.Unicode)]
    internal static extern bool FindNextFile(
        nint hFindFile,
        out NativeModels.Win32FindData lpFindFileData
    );

    [DllImport("api-ms-win-core-file-l1-1-0.dll")]
    internal static extern bool FindClose(nint hFindFile);

    [DllImport("Shcore.dll", SetLastError = true)]
    internal static extern int GetDpiForMonitor(nint hmonitor, NativeModels.MonitorDPIType dpiType, out uint dpiX, out uint dpiY);

    [DllImport("user32.dll")]
    internal static extern bool GetWindowPlacement(nint hWnd, out NativeModels.WindowPlacement lpwndpl);

    [DllImport("user32.dll")]
    internal static extern bool SetWindowPlacement(nint hWnd, [In] ref NativeModels.WindowPlacement lpwndpl);

    [DllImport("user32.dll")]
    internal static extern IntPtr SetWinEventHook(uint eventMin, uint eventMax, IntPtr eventHookAssemblyHandle, NativeModels.WinEventDelegate eventHookHandle, uint processId, uint threadId, uint dwFlags);

    [DllImport("user32.dll")]
    internal static extern bool UnhookWinEvent(IntPtr hWinEventHook);

    [DllImport("gdi32.dll")]
    internal static extern int GetDeviceCaps(IntPtr hdc, int nIndex);
}
