// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;

using ComicReader.SDK.Common.DebugTools;

using Windows.ApplicationModel;
using Windows.System.UserProfile;

namespace ComicReader.Common.AppEnvironment;

internal class EnvironmentProvider
{
    public static EnvironmentProvider Instance = new();

    private readonly DateTimeOffset _launchTime;

    private EnvironmentProvider()
    {
        _launchTime = DateTimeOffset.Now;
    }

    public void AppendEnvironmentTags(Dictionary<string, string> tags)
    {
        tags["version-name"] = GetVersionName();
        tags["os-build"] = DeviceInformationHelper.Instance.GetOsBuild();
        tags["os-version"] = DeviceInformationHelper.Instance.GetOsVersion();
        tags["lang-installed"] = CultureInfo.InstalledUICulture.Name;
        tags["lang-current"] = CultureInfo.CurrentUICulture.Name;
        tags["device-model"] = DeviceInformationHelper.Instance.GetDeviceModel();
        tags["oem-name"] = DeviceInformationHelper.Instance.GetDeviceOemName();
        tags["screen-size"] = DeviceInformationHelper.Instance.GetScreenSize();
        tags["processor-count"] = Environment.ProcessorCount.ToString();
    }

    public void AppendDebugText(StringBuilder sb)
    {
        sb.SafeAppend("OS build", DeviceInformationHelper.Instance.GetOsBuild);
        sb.SafeAppend("OS version", DeviceInformationHelper.Instance.GetOsVersion);
        sb.SafeAppend("OS architecture", () => RuntimeInformation.OSArchitecture);
        sb.SafeAppend("Installed system language", () => CultureInfo.InstalledUICulture.Name);
        sb.SafeAppend("Current system language", GetCurrentSystemLanguage);
        sb.SafeAppend("Current app language", () => CultureInfo.CurrentUICulture.Name);
        sb.SafeAppend("Machine name", () => Environment.MachineName);
        sb.SafeAppend("Device model", DeviceInformationHelper.Instance.GetDeviceModel);
        sb.SafeAppend("OEM name", DeviceInformationHelper.Instance.GetDeviceOemName);
        sb.SafeAppend("Processor count", () => Environment.ProcessorCount);
        sb.SafeAppend("Screen size", DeviceInformationHelper.Instance.GetScreenSize);
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

    public string GetCurrentSystemLanguage()
    {
        return GlobalizationPreferences.Languages[0];
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
