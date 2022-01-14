using Microsoft.Win32.SafeHandles;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace ComicReader.Utils
{
    public class Win32IO
    {
        // System Error Codes
        // https://docs.microsoft.com/en-us/windows/win32/debug/system-error-codes--0-499-?redirectedfrom=MSDN
        internal const int ERROR_ACCESS_DENIED = 5;

        internal const int FIND_FIRST_EX_CASE_SENSITIVE = 1;
        internal const int FIND_FIRST_EX_LARGE_FETCH = 2;
        internal const int FIND_FIRST_EX_ON_DISK_ENTRIES_ONLY = 4;

        internal const int GENERIC_READ = unchecked((int)0x80000000);
        internal const int GENERIC_ALL = unchecked((int)0x10000000);

        internal const int CREATE_NEW = 1;
        internal const int CREATE_ALWAYS = 2;
        internal const int OPEN_EXISTING = 3;
        internal const int OPEN_ALWAYS = 4;
        internal const int TRUNCATE_EXISTING = 5;

        internal const int FILE_ATTRIBUTE_NORMAL = 0x80;

        // https://docs.microsoft.com/en-us/windows/win32/api/minwinbase/ne-minwinbase-findex_info_levels
        internal enum FindExInfoLevel
        {
            FindExInfoStandard = 0,
            FindExInfoBasic = 1
        }

        // https://docs.microsoft.com/en-us/windows/win32/api/minwinbase/ne-minwinbase-findex_search_ops
        internal enum FIndexSearchOps
        {
            FindExSearchNameMatch = 0,
            FindExSearchLimitToDirectories = 1,
            FindExSearchLimitToDevices = 2,
            FindExSearchMaxSearchOp = 3,
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        internal struct WIN32_FIND_DATA
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
        static internal extern IntPtr FindFirstFileExFromApp(
            string lpFileName,
            FindExInfoLevel fInfoLevelId,
            out WIN32_FIND_DATA lpFindFileData,
            FIndexSearchOps fSearchOp,
            IntPtr lpSearchFilter,
            int dwAdditionalFlags);

        [DllImport("api-ms-win-core-file-l1-1-0.dll", CharSet = CharSet.Unicode)]
        static internal extern bool FindNextFile(IntPtr hFindFile, out WIN32_FIND_DATA lpFindFileData);

        [DllImport("api-ms-win-core-file-l1-1-0.dll")]
        static internal extern bool FindClose(IntPtr hFindFile);

        // https://docs.microsoft.com/en-us/windows/win32/api/fileapifromapp/nf-fileapifromapp-createfilefromappw
        [DllImport("api-ms-win-core-file-fromapp-l1-1-0.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        static internal extern SafeFileHandle CreateFileFromApp(string lpFileName,
            int dwDesiredAccess,
            int dwShareMode,
            IntPtr lpSecurityAttributes,
            uint dwCreationDisposition,
            uint dwFlagsAndAttributes,
            SafeFileHandle hTemplateFile);

        // SubFolderDeep
        public class SubFolderDeepContext
        {
            public SubFolderDeepContext(string path)
            {
                Nodes.Add(new Node
                {
                    Paths = new List<string> { path }
                });

                if (Environment.OSVersion.Version.Major >= 6)
                {
                    FindInfoLevel = FindExInfoLevel.FindExInfoBasic;
                    AdditionalFlags = FIND_FIRST_EX_LARGE_FETCH;
                }
                else
                {
                    FindInfoLevel = FindExInfoLevel.FindExInfoStandard;
                    AdditionalFlags = 0;
                }
            }

            private class Node
            {
                public List<string> Paths = new List<string>();
                public int Index = 0;

                public string CurrentPath => Paths[Index];
            }

            public List<string> NoAccessFolders { get; private set; } = new List<string>();

            private List<Node> Nodes = new List<Node>();
            private readonly FindExInfoLevel FindInfoLevel;
            private readonly FIndexSearchOps IndexSearchOps = FIndexSearchOps.FindExSearchNameMatch;
            private readonly int AdditionalFlags;
            private int FolderScanned = 0;

            public bool Search(List<string> results, uint min_step)
            {
                results.Clear();

                if (Nodes.Count == 0)
                {
                    return false;
                }

                FolderScanned = 0;

                bool not_end = _Search(min_step, results);

                if (results.Count > 0)
                {
                    return true;
                }

                return not_end;
            }

            private bool _Search(uint min_step, List<string> results, int depth = 0)
            {
                if (Nodes.Count <= depth)
                {
                    // Visit current node.
                    string path_raw = Nodes[Nodes.Count - 1].CurrentPath;
                    string path = path_raw + "\\";
                    IntPtr h_file = FindFirstFileExFromApp(path + "*", FindInfoLevel,
                        out _, IndexSearchOps, IntPtr.Zero, AdditionalFlags);

                    if (h_file.ToInt64() == -1)
                    {
                        int error_code = Marshal.GetLastWin32Error();
                        Utils.Debug.Log("Failed to access '" + path_raw + "', error code " + error_code.ToString() + ".");

                        if (error_code == ERROR_ACCESS_DENIED)
                        {
                            NoAccessFolders.Add(path_raw);
                        }

                        return false;
                    }

                    int i_begin = results.Count;
                    FindNextFile(h_file, out _);

                    while (FindNextFile(h_file, out WIN32_FIND_DATA find_data))
                    {
                        if (((FileAttributes)find_data.dwFileAttributes & FileAttributes.Directory) == FileAttributes.Directory)
                        {
                            results.Add(path + find_data.cFileName);
                        }
                    }

                    FindClose(h_file);
                    int i_end = results.Count;
                    int item_count = i_end - i_begin;

                    if (item_count == 0)
                    {
                        return false;
                    }

                    Nodes.Add(new Node
                    {
                        Paths = results.GetRange(i_begin, item_count)
                    });
                }

                // Search deeper.
                while (Nodes[depth].Index < Nodes[depth].Paths.Count)
                {
                    // Exit if min_step is reached.
                    if (FolderScanned + results.Count >= min_step)
                    {
                        return true;
                    }

                    if (_Search(min_step, results, depth + 1))
                    {
                        return true;
                    }

                    Nodes[depth].Index++;
                    FolderScanned++;
                }

                Nodes.RemoveAt(Nodes.Count - 1);
                return false;
            }
        }

        // SubFiles
        public static List<string> SubFiles(string path, string name)
        {
            FindExInfoLevel FindInfoLevel;
            FIndexSearchOps IndexSearchOps = FIndexSearchOps.FindExSearchNameMatch;
            int AdditionalFlags;

            if (Environment.OSVersion.Version.Major >= 6)
            {
                FindInfoLevel = FindExInfoLevel.FindExInfoBasic;
                AdditionalFlags = FIND_FIRST_EX_LARGE_FETCH;
            }
            else
            {
                FindInfoLevel = FindExInfoLevel.FindExInfoStandard;
                AdditionalFlags = 0;
            }

            path += "\\";
            List<string> results = new List<string>();

            IntPtr h_file = FindFirstFileExFromApp(path + name, FindInfoLevel,
                out _, IndexSearchOps, IntPtr.Zero, AdditionalFlags);

            if (h_file.ToInt64() == -1)
            {
                return results;
            }

            FindNextFile(h_file, out _);
            while (FindNextFile(h_file, out WIN32_FIND_DATA find_data))
            {
                if (((FileAttributes)find_data.dwFileAttributes & FileAttributes.Directory) != FileAttributes.Directory)
                {
                    results.Add(path + find_data.cFileName);
                }
            }
            FindClose(h_file);

            return results;
        }

        public static async Task<string> ReadFileFromPath(string path)
        {
            SafeFileHandle h = CreateFileFromApp(path, GENERIC_READ, 0, IntPtr.Zero, OPEN_EXISTING, FILE_ATTRIBUTE_NORMAL, new SafeFileHandle(IntPtr.Zero, true));
            
            if (h.IsInvalid)
            {
                throw new FileNotFoundException();
            }

            using (FileStream stream = new FileStream(h, FileAccess.Read))
            using (StreamReader reader = new StreamReader(stream))
            {
                string text = await reader.ReadToEndAsync();
                return text;
            }
        }
    }
}