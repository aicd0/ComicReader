// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;

using ComicReader.Common.DebugTools;

using Windows.ApplicationModel;

namespace ComicReader.Common.AppEnvironment;

internal class EnvironmentProvider
{
    public static EnvironmentProvider Instance = new();

    private readonly DateTimeOffset _launchTime;

    private EnvironmentProvider()
    {
        _launchTime = DateTimeOffset.Now;
    }

    public void Initialize()
    {
    }

    public void AppendDebugText(StringBuilder sb)
    {
        sb.SafeAppend("OS version", () => Environment.OSVersion);
        sb.SafeAppend("OS architecture", () => RuntimeInformation.OSArchitecture);
        sb.SafeAppend("Installed language", () => CultureInfo.InstalledUICulture.Name);
        sb.SafeAppend("Current language", () => CultureInfo.CurrentUICulture.Name);
        sb.SafeAppend("Machine name", () => Environment.MachineName);
        sb.SafeAppend("Processor count", () => Environment.ProcessorCount);
        sb.SafeAppend("Version name", GetVersionName);
        sb.SafeAppend("Build type", () => DebugUtils.DebugBuild ? "Debug" : "Release");
        sb.SafeAppend("Process architecture", () => RuntimeInformation.ProcessArchitecture);
        sb.SafeAppend("Launch time", () => GetLaunchTime());
        sb.SafeAppend("Awake time", () => GetAwakeTime());

        string additionalInfo = Properties.AdditionalDebugInformation;
        if (additionalInfo.Length > 0)
        {
            sb.Append(additionalInfo);
            sb.Append('\n');
        }
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
