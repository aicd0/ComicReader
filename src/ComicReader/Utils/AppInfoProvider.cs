using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ComicReader.Utils
{
    public class AppInfoProvider
    {
        public static readonly List<string> SupportedFileTypes = new List<string>{
            ".jpg",
            ".jpeg",
            ".png",
            ".bmp",
        };

        public static bool IsSupportedFileType(string filename)
        {
            string extension = Utils.StringUtils.FilenameExtensionFromFilename(filename).ToLower();

            switch (extension)
            {
                case "jpg":
                case "jpeg":
                case "png":
                case "bmp":
                    return true;
                default:
                    return false;
            }
        }
    }
}
