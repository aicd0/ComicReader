using Microsoft.Win32.SafeHandles;
using System;
using System.Runtime.InteropServices;

namespace ComicReader.Native
{
    internal class NativeMethods
    {
        // https://docs.microsoft.com/en-us/windows/win32/api/fileapifromapp/nf-fileapifromapp-createfilefromappw
        [DllImport("api-ms-win-core-file-fromapp-l1-1-0.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        internal static extern SafeFileHandle CreateFileFromApp(string lpFileName,
            int dwDesiredAccess,
            int dwShareMode,
            IntPtr lpSecurityAttributes,
            uint dwCreationDisposition,
            uint dwFlagsAndAttributes,
            SafeFileHandle hTemplateFile
        );

        // https://docs.microsoft.com/en-us/windows/win32/api/fileapifromapp/nf-fileapifromapp-findfirstfileexfromappw
        [DllImport("api-ms-win-core-file-fromapp-l1-1-0.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        internal static extern IntPtr FindFirstFileExFromApp(
            string lpFileName,
            NativeModels.FindExInfoLevel fInfoLevelId,
            out NativeModels.Win32FindData lpFindFileData,
            NativeModels.FIndexSearchOps fSearchOp,
            IntPtr lpSearchFilter,
            int dwAdditionalFlags
        );

        [DllImport("api-ms-win-core-file-l1-1-0.dll", CharSet = CharSet.Unicode)]
        internal static extern bool FindNextFile(
            IntPtr hFindFile,
            out NativeModels.Win32FindData lpFindFileData
        );

        [DllImport("api-ms-win-core-file-l1-1-0.dll")]
        internal static extern bool FindClose(IntPtr hFindFile);

        [DllImport("Shcore.dll", SetLastError = true)]
        internal static extern int GetDpiForMonitor(IntPtr hmonitor, NativeModels.MonitorDPIType dpiType, out uint dpiX, out uint dpiY);

        [DllImport("user32.dll")]
        internal static extern bool GetWindowPlacement(IntPtr hWnd, out NativeModels.WindowPlacement lpwndpl);

        [DllImport("user32.dll")]
        internal static extern bool SetWindowPlacement(IntPtr hWnd, [In] ref NativeModels.WindowPlacement lpwndpl);
    }
}
