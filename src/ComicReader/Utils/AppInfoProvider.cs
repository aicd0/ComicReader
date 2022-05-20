﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ComicReader.Utils
{
    public class AppInfoProvider
    {
        // File types.
        public static readonly HashSet<string> SupportedImageExtensions = new HashSet<string>{
            ".bmp",
            ".gif",
            ".jpg",
            ".jpe",
            ".jpeg",
            ".png",
            ".tif",
            ".tiff",
            ".webp",
        };

        public static readonly HashSet<string> SupportedArchiveExtensions = new HashSet<string>{
            ".7z",
            ".bz2",
            ".gz",
            ".rar",
            ".tar",
            ".xz",
            ".zip",
        };

        public static readonly HashSet<string> SupportedDocumentExtensions = new HashSet<string>{
            ".pdf",
        };

        private static HashSet<string> m_SupportedExternalFileExtensions = null;
        public static HashSet<string> SupportedExternalFileExtensions {
            get
            {
                if (m_SupportedExternalFileExtensions == null)
                {
                    m_SupportedExternalFileExtensions = new HashSet<string>();
                    m_SupportedExternalFileExtensions.UnionWith(SupportedImageExtensions);
                    m_SupportedExternalFileExtensions.UnionWith(SupportedArchiveExtensions);
                    m_SupportedExternalFileExtensions.UnionWith(SupportedDocumentExtensions);
                }
                return m_SupportedExternalFileExtensions;
            }
        }

        public static bool IsSupportedExternalFileExtension(string extension)
        {
            return SupportedExternalFileExtensions.Contains(extension.ToLower());
        }

        public static bool IsSupportedImageExtension(string extension)
        {
            return SupportedImageExtensions.Contains(extension.ToLower());
        }

        public static bool IsSupportedArchiveExtension(string extension)
        {
            return SupportedArchiveExtensions.Contains(extension.ToLower());
        }

        public static bool IsSupportedDocumentExtension(string extension)
        {
            return SupportedDocumentExtensions.Contains(extension.ToLower());
        }

        // Encodings.
        private static Dictionary<int, Encoding> m_SupportedEncodings = null;
        public static Dictionary<int, Encoding> SupportedEncodings
        {
            get
            {
                if (m_SupportedEncodings == null)
                {
                    // Encoding.GetEncodings() doesn't return the full list of encodings.
                    // At least it doesn't work for current version of UWP.
                    // ref: https://github.com/dotnet/runtime/issues/25819
                    // So we hard-coded the whole list instead.

                    Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

                    // ref: https://docs.microsoft.com/en-us/windows/win32/intl/code-page-identifiers
                    int[] codepages = new int[] {
                        37, 437, 500, 708, 709, 710, 720, 737, 775, 850, 852, 855, 857, 858,
                        860, 861, 862, 863, 864, 865, 866, 869, 870, 874, 875, 932, 936, 949,
                        950, 1026, 1047, 1140, 1141, 1142, 1143, 1144, 1145, 1146, 1147, 1148,
                        1149, 1200, 1201, 1250, 1251, 1252, 1253, 1254, 1255, 1256, 1257, 1258,
                        1361, 10000, 10001, 10002, 10003, 10004, 10005, 10006, 10007, 10008,
                        10010, 10017, 10021, 10029, 10079, 10081, 10082, 12000, 12001, 20000,
                        20001, 20002, 20003, 20004, 20005, 20105, 20106, 20107, 20108, 20127,
                        20261, 20269, 20273, 20277, 20278, 20280, 20284, 20285, 20290, 20297,
                        20420, 20423, 20424, 20833, 20838, 20866, 20871, 20880, 20905, 20924,
                        20932, 20936, 20949, 21025, 21027, 21866, 28591, 28592, 28593, 28594,
                        28595, 28596, 28597, 28598, 28599, 28603, 28605, 29001, 38598, 50220,
                        50221, 50222, 50225, 50227, 50229, 50930, 50931, 50933, 50935, 50936,
                        50937, 50939, 51932, 51936, 51949, 51950, 52936, 54936, 57002, 57003,
                        57004, 57005, 57006, 57007, 57008, 57009, 57010, 57011, 65000, 65001,
                    };

                    m_SupportedEncodings = new Dictionary<int, Encoding>();

                    foreach (int codepage in codepages)
                    {
                        try
                        {
                            Encoding info = Encoding.GetEncoding(codepage);
                            m_SupportedEncodings.Add(codepage, info);
                        }
                        catch { }
                    }
                }
                return m_SupportedEncodings;
            }
        }
    }
}
