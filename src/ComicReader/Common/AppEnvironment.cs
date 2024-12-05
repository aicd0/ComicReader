// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;

using Windows.ApplicationModel;

namespace ComicReader.Common;

internal class AppEnvironment
{
    public static AppEnvironment Instance = new();

    private readonly DateTimeOffset _launchTime;

    private AppEnvironment()
    {
        _launchTime = DateTimeOffset.Now;
    }

    public void Initialize()
    {
    }

    public string GetVersionName()
    {
        PackageVersion version = Package.Current.Id.Version;
        return version.Major + "." + version.Minor + "." + version.Build + "." + version.Revision;
    }

    public DateTimeOffset GetLaunchTime()
    {
        return _launchTime;
    }

    public TimeSpan GetAwakeTime()
    {
        return DateTimeOffset.Now - _launchTime;
    }
}
