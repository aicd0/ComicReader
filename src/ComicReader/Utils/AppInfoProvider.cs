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
            ".jpeg",
            ".png",
            ".bmp",
        };

        public static bool IsSupportedFileExtension(string extension)
        {
            switch (extension.ToLower())
            {
                case ".jpg":
                case ".jpeg":
                case ".png":
                case ".bmp":
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
                case ".jpeg":
                case ".png":
                case ".bmp":
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
