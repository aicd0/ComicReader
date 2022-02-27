using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.ApplicationModel.Resources;

namespace ComicReader.Utils
{
    class StringResourceProvider
    {
        private static Dictionary<string, string> ResourceStrings = new Dictionary<string, string>();

        public static string GetResourceString(string resource)
        {
            if (ResourceStrings.ContainsKey(resource))
            {
                return ResourceStrings[resource];
            }

            ResourceLoader resource_loader = ResourceLoader.GetForCurrentView();

            if (resource_loader == null)
            {
                return "?";
            }

            string res = resource_loader.GetString(resource);
            ResourceStrings[resource] = res;
            return res;
        }
    }
}
