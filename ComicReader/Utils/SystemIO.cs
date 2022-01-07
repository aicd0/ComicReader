using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace ComicReader.Utils
{
    public class SystemIO
    {
        // https://docs.microsoft.com/en-us/windows/win32/api/minwinbase/ne-minwinbase-findex_info_levels
        public enum FindExInfoLevel
        {
            FindExInfoStandard = 0,
            FindExInfoBasic = 1
        }

        // https://docs.microsoft.com/en-us/windows/win32/api/minwinbase/ne-minwinbase-findex_search_ops
        private enum FINDEX_SEARCH_OPS
        {
            FindExSearchNameMatch = 0,
            FindExSearchLimitToDirectories = 1,
            FindExSearchLimitToDevices = 2,
            FindExSearchMaxSearchOp = 3,
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        private struct WIN32_FIND_DATA
        {
            public uint dwFileAttributes;
            public System.Runtime.InteropServices.ComTypes.FILETIME ftCreationTime;
            public System.Runtime.InteropServices.ComTypes.FILETIME ftLastAccessTime;
            public System.Runtime.InteropServices.ComTypes.FILETIME ftLastWriteTime;
            public uint nFileSizeHigh;
            public uint nFileSizeLow;
            public uint dwReserved0;
            public uint dwReserved1;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
            public string cFileName;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 14)]
            public string cAlternateFileName;
        }

        // https://docs.microsoft.com/en-us/windows/win32/api/fileapifromapp/nf-fileapifromapp-findfirstfileexfromappw
        [DllImport("api-ms-win-core-file-fromapp-l1-1-0.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern IntPtr FindFirstFileExFromApp(
            string lpFileName,
            FindExInfoLevel fInfoLevelId,
            out WIN32_FIND_DATA lpFindFileData,
            FINDEX_SEARCH_OPS fSearchOp,
            IntPtr lpSearchFilter,
            int dwAdditionalFlags);

        public const int FIND_FIRST_EX_CASE_SENSITIVE = 1;
        public const int FIND_FIRST_EX_LARGE_FETCH = 2;
        public const int FIND_FIRST_EX_ON_DISK_ENTRIES_ONLY = 4;

        [DllImport("api-ms-win-core-file-l1-1-0.dll", CharSet = CharSet.Unicode)]
        private static extern bool FindNextFile(IntPtr hFindFile, out WIN32_FIND_DATA lpFindFileData);

        [DllImport("api-ms-win-core-file-l1-1-0.dll")]
        private static extern bool FindClose(IntPtr hFindFile);

        public static List<string> SearchFolder(string pattern)
        {
            FindExInfoLevel find_info_level = FindExInfoLevel.FindExInfoStandard;
            int additional_flags = 0;

            if (Environment.OSVersion.Version.Major >= 6)
            {
                find_info_level = FindExInfoLevel.FindExInfoBasic;
                additional_flags = FIND_FIRST_EX_LARGE_FETCH;
            }

            IntPtr hFile = FindFirstFileExFromApp(pattern, find_info_level, out WIN32_FIND_DATA find_data,
                FINDEX_SEARCH_OPS.FindExSearchNameMatch, IntPtr.Zero, additional_flags);

            if (hFile.ToInt64() == -1)
            {
                return null;
            }

            List<string> file_names = new List<string>();

            do
            {
                if (((FileAttributes)find_data.dwFileAttributes & FileAttributes.Directory) == FileAttributes.Directory)
                {
                    string fn = find_data.cFileName;
                    file_names.Add(fn);
                }
            } while (FindNextFile(hFile, out find_data));

            FindClose(hFile);
            return file_names;
        }
    }
}