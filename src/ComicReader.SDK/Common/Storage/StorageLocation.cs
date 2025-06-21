// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using ComicReader.SDK.Common.ServiceManagement;

using Windows.Storage;

namespace ComicReader.SDK.Common.Storage;

public static class StorageLocation
{
    private const string DIR_USER = "user";

    private static readonly Lazy<bool> Portable = new(() =>
    {
        return ServiceManager.GetService<IApplicationService>().IsPortableBuild();
    }, true);

    public static string GetLocalFolderPath()
    {
        if (Portable.Value)
        {
            return Path.Combine(GetDeploymentPath(), DIR_USER, "local");
        }
        else
        {
            return ApplicationData.Current.LocalFolder.Path;
        }
    }

    public static string GetLocalCacheFolderPath()
    {
        if (Portable.Value)
        {
            return Path.Combine(GetDeploymentPath(), DIR_USER, "local_cache");
        }
        else
        {
            return ApplicationData.Current.LocalCacheFolder.Path;
        }
    }

    public static string GetTemporaryFolderPath()
    {
        if (Portable.Value)
        {
            return Path.Combine(GetDeploymentPath(), DIR_USER, "temporary");
        }
        else
        {
            return ApplicationData.Current.TemporaryFolder.Path;
        }
    }

    private static string GetDeploymentPath()
    {
        return AppContext.BaseDirectory;
    }
}
