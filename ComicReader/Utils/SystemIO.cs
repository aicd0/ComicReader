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
        public const int FIND_FIRST_EX_CASE_SENSITIVE = 1;
        public const int FIND_FIRST_EX_LARGE_FETCH = 2;
        public const int FIND_FIRST_EX_ON_DISK_ENTRIES_ONLY = 4;

        // https://docs.microsoft.com/en-us/windows/win32/api/minwinbase/ne-minwinbase-findex_info_levels
        public enum FindExInfoLevel
        {
            FindExInfoStandard = 0,
            FindExInfoBasic = 1
        }

        // https://docs.microsoft.com/en-us/windows/win32/api/minwinbase/ne-minwinbase-findex_search_ops
        public enum FIndexSearchOps
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
            FIndexSearchOps fSearchOp,
            IntPtr lpSearchFilter,
            int dwAdditionalFlags);

        [DllImport("api-ms-win-core-file-l1-1-0.dll", CharSet = CharSet.Unicode)]
        private static extern bool FindNextFile(IntPtr hFindFile, out WIN32_FIND_DATA lpFindFileData);

        [DllImport("api-ms-win-core-file-l1-1-0.dll")]
        private static extern bool FindClose(IntPtr hFindFile);

        // SubFoldersDeep
        public class SubFoldersDeepSearchContextNode
        {
            public List<string> Paths = new List<string>();
            public int Index = 0;

            public string CurrentPath => Paths[Index];
        }

        public class SubFoldersDeepSearchContext
        {
            public SubFoldersDeepSearchContext(string path)
            {
                Nodes.Add(new SubFoldersDeepSearchContextNode
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

            public List<SubFoldersDeepSearchContextNode> Nodes = new List<SubFoldersDeepSearchContextNode>();
            public readonly FindExInfoLevel FindInfoLevel;
            public readonly FIndexSearchOps IndexSearchOps = FIndexSearchOps.FindExSearchNameMatch;
            public readonly int AdditionalFlags;
            public int _FolderScanned = 0;
        };

        public static bool SubFoldersDeep(SubFoldersDeepSearchContext ctx, out List<string> results, uint min_step)
        {
            results = new List<string>();

            if (ctx.Nodes.Count == 0)
            {
                return false;
            }

            ctx._FolderScanned = 0;
            return _SubFoldersDeep(ctx, min_step, results);
        }

        private static bool _SubFoldersDeep(SubFoldersDeepSearchContext ctx, uint min_step, List<string> results, int depth = 0)
        {
            if (ctx.Nodes.Count <= depth)
            {
                // Visit current node.
                string path = ctx.Nodes[ctx.Nodes.Count - 1].CurrentPath + "\\";
                IntPtr h_file = FindFirstFileExFromApp(path + "*", ctx.FindInfoLevel,
                    out _, ctx.IndexSearchOps, IntPtr.Zero, ctx.AdditionalFlags);

                if (h_file.ToInt64() == -1)
                {
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

                if (i_begin == i_end)
                {
                    return false;
                }

                ctx.Nodes.Add(new SubFoldersDeepSearchContextNode
                {
                    Paths = results.GetRange(i_begin, i_end - i_begin)
                });
            }

            // Search deeper.
            while (ctx.Nodes[depth].Index < ctx.Nodes[depth].Paths.Count)
            {
                // Exit if min_step is reached.
                if (ctx._FolderScanned + results.Count >= min_step)
                {
                    return true;
                }

                if (_SubFoldersDeep(ctx, min_step, results, depth + 1))
                {
                    return true;
                }

                ctx.Nodes[depth].Index++;
                ctx._FolderScanned++;
            }

            ctx.Nodes.RemoveAt(ctx.Nodes.Count - 1);
            return false;
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
    }
}