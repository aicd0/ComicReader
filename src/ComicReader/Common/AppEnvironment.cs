// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;

using ComicReader.Common.DebugTools;

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

    public void AppendDebugText(StringBuilder sb)
    {
        try
        {
            sb.Append("OS version: ");
            sb.Append(Environment.OSVersion);
            sb.Append('\n');

            sb.Append("OS architecture: ");
            sb.Append(RuntimeInformation.OSArchitecture);
            sb.Append('\n');

            sb.Append("Machine name: ");
            sb.Append(Environment.MachineName);
            sb.Append('\n');

            sb.Append("Process architecture: ");
            sb.Append(RuntimeInformation.ProcessArchitecture);
            sb.Append('\n');

            sb.Append("Process count: ");
            sb.Append(Environment.ProcessorCount);
            sb.Append('\n');

            sb.Append("Build type: ");
            sb.Append(DebugUtils.DebugBuild ? "Debug" : "Release");
            sb.Append('\n');

            sb.Append("Version name: ");
            sb.Append(GetVersionName());
            sb.Append('\n');

            sb.Append("Launch time: ");
            sb.Append(GetLaunchTime());
            sb.Append('\n');

            sb.Append("Awake time: ");
            sb.Append(GetAwakeTime());
            sb.Append('\n');

            sb.Append("Installed system language: ");
            sb.Append(CultureInfo.InstalledUICulture.Name);
            sb.Append('\n');

            sb.Append("Current system language: ");
            sb.Append(CultureInfo.CurrentCulture.Name);
            sb.Append('\n');

            string additionalInfo = Properties.AdditionalDebugInformation;
            if (additionalInfo.Length > 0)
            {
                sb.Append(additionalInfo);
                sb.Append('\n');
            }
        }
        catch (Exception)
        {
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
