using Microsoft.Windows.ApplicationModel.Resources;

namespace ComicReader.Utils;

class StringResourceProvider
{
    private static readonly ResourceLoader sResourceLoader = new();

    public static string GetResourceString(string resource)
    {
        return sResourceLoader.GetString(resource) ?? "?";
    }
}
