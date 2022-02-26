using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ComicReader.Utils
{
    public class AppInfoProvider
    {
        public static readonly List<string> SupportedImageTypes = new List<string>{
            ".jpg",
            ".jpe",
            ".jpeg",
            ".png",
            ".bmp",
            ".gif",
        };

        public static bool IsSupportedFileExtension(string extension)
        {
            switch (extension.ToLower())
            {
                case ".jpg":
                case ".jpe":
                case ".jpeg":
                case ".png":
                case ".bmp":
                case ".gif":
                case ".zip":
                    return true;
                default:
                    return false;
            }
        }

        public static bool IsSupportedImageExtension(string extension)
        {
            switch (extension.ToLower())
            {
                case ".jpg":
                case ".jpe":
                case ".jpeg":
                case ".png":
                case ".bmp":
                case ".gif":
                    return true;
                default:
                    return false;
            }
        }

        public static bool IsSupportedComicExtension(string extension)
        {
            switch (extension.ToLower())
            {
                case ".zip":
                    return true;
                default:
                    return false;
            }
        }
    }
}
