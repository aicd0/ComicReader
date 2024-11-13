// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

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
