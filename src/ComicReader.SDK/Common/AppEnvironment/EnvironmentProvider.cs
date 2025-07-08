// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System.Globalization;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;

using ComicReader.SDK.Common.DebugTools;
using ComicReader.SDK.Common.ServiceManagement;
using ComicReader.SDK.Common.Utils;

using Windows.ApplicationModel;
using Windows.Globalization;
using Windows.System.UserProfile;

namespace ComicReader.SDK.Common.AppEnvironment;

public class EnvironmentProvider
{
    public static EnvironmentProvider Instance = new();

    private readonly DateTimeOffset _launchTime;
    private string _additionalDebugInformation = string.Empty;

    private EnvironmentProvider()
    {
        _launchTime = DateTimeOffset.Now;
    }

    /// <summary>
    /// Initialize the EnvironmentProvider instance. Some fields like launch time
    /// require the static instance to be initialized as soon as possible.
    /// </summary>
    public void Initialize(string additionalDebugInformation)
    {
        _additionalDebugInformation = additionalDebugInformation;
    }

    public void AppendDebugText(StringBuilder sb)
    {
        sb.SafeAppend("OS build", DeviceInformationHelper.Instance.GetOsBuild);
        sb.SafeAppend("OS version", DeviceInformationHelper.Instance.GetOsVersion);
        sb.SafeAppend("OS architecture", () => RuntimeInformation.OSArchitecture);
        sb.SafeAppend("Installed system language", () => CultureInfo.InstalledUICulture.Name);
        sb.SafeAppend("Current system language", GetCurrentSystemLanguage);
        sb.SafeAppend("Current app language", GetCurrentAppLanguage);
        sb.SafeAppend("Machine name", () => Environment.MachineName);
        sb.SafeAppend("Device model", DeviceInformationHelper.Instance.GetDeviceModel);
        sb.SafeAppend("OEM name", DeviceInformationHelper.Instance.GetDeviceOemName);
        sb.SafeAppend("Processor count", () => Environment.ProcessorCount);
        sb.SafeAppend("Screen size", DeviceInformationHelper.Instance.GetScreenSize);
        sb.SafeAppend("Version name", GetVersionName);
        sb.SafeAppend("Build type", () => DebugUtils.DebugBuild ? "Debug" : "Release");
        sb.SafeAppend("Portable", () => IsPortable());
        sb.SafeAppend("Process architecture", () => RuntimeInformation.ProcessArchitecture);
        sb.SafeAppend("Launch time", () => GetLaunchTime());
        sb.SafeAppend("Awake time", () => GetAwakeTime());

        if (_additionalDebugInformation.Length > 0)
        {
            sb.Append(_additionalDebugInformation);
            sb.Append('\n');
        }
    }

    public DateTimeOffset GetLaunchTime()
    {
        return _launchTime;
    }

    public TimeSpan GetAwakeTime()
    {
        return DateTimeOffset.Now - _launchTime;
    }

    public static Dictionary<string, string> GetEnvironmentTags()
    {
        Dictionary<string, string> tags = [];
        tags["version-name"] = GetVersionName();
        tags["portable"] = IsPortable() ? "true" : "false";
        return tags;
    }

    public static string GetVersionName()
    {
        if (IsPortable())
        {
            Version? version = Assembly.GetEntryAssembly()?.GetName().Version;
            if (version == null)
            {
                return "0.0.0.0";
            }
            return $"{version.Major}.{version.Minor}.{version.Build}.{version.Revision}";
        }
        else
        {
            PackageVersion version = Package.Current.Id.Version;
            return $"{version.Major}.{version.Minor}.{version.Build}.{version.Revision}";
        }
    }

    public static string GetCurrentSystemLanguage()
    {
        return GlobalizationPreferences.Languages[0];
    }

    public static string GetCurrentAppLanguage()
    {
        string language = ApplicationLanguages.PrimaryLanguageOverride;
        if (string.IsNullOrEmpty(language))
        {
            language = CultureInfo.CurrentUICulture.Name;
        }
        return language;
    }

    public static CultureInfo GetCurrentAppLanguageInfo()
    {
        string language = GetCurrentAppLanguage();
        if (string.IsNullOrEmpty(language))
        {
            return CultureInfo.CurrentUICulture;
        }
        try
        {
            return new CultureInfo(language);
        }
        catch (CultureNotFoundException)
        {
            Logger.AssertNotReachHere("92052DFC4960E58D", language);
            return CultureInfo.CurrentUICulture;
        }
    }

    public static bool IsPortable()
    {
        return ServiceManager.GetService<IApplicationService>().IsPortableBuild();
    }
}
