using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ComicReader.Utils
{
    public class AppInfoProvider
    {
        public static readonly HashSet<string> SupportedImageExtensions = new HashSet<string>{
            ".jpg",
            ".jpe",
            ".jpeg",
            ".png",
            ".bmp",
            ".gif",
            ".tif",
            ".tiff",
            ".webp",
        };

        public static readonly HashSet<string> SupportedComicExtensions = new HashSet<string>{
            ".zip",
        };

        private static HashSet<string> m_SupportedExternalFileExtensions = null;
        public static HashSet<string> SupportedExternalFileExtensions {
            get
            {
                if (m_SupportedExternalFileExtensions == null)
                {
                    m_SupportedExternalFileExtensions = new HashSet<string>();
                    m_SupportedExternalFileExtensions.UnionWith(SupportedImageExtensions);
                    m_SupportedExternalFileExtensions.UnionWith(SupportedComicExtensions);
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

        public static bool IsSupportedComicExtension(string extension)
        {
            return SupportedComicExtensions.Contains(extension.ToLower());
        }
    }
}
