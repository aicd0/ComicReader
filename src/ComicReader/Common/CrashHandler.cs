// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

using ComicReader.Common.DebugTools;

using Windows.Storage;

namespace ComicReader.Common;

internal static class CrashHandler
{
    public static void OnUnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs e)
    {
        try
        {
            StringBuilder sb = new();

            sb.Append("Message:\n");
            sb.Append(e.Message);
            sb.Append("\n\n");

            sb.Append("Stack trace:\n");
            sb.Append(e.Exception.ToString());
            sb.Append("\n\n");

            try
            {
                AppendSystemInformation(sb);
            }
            catch (Exception ex)
            {
                Console(ex.ToString());
            }

            string fileName = $"crash_report_{DateTimeOffset.Now:yyyyMMddHHmmss}.txt";
            string filePath = ApplicationData.Current.LocalCacheFolder.Path + "\\" + fileName;
            using StreamWriter writer = new(filePath, true, Encoding.UTF8);
            writer.Write(sb.ToString());
        }
        catch (Exception ex)
        {
            Console(ex.ToString());
        }

        try
        {
            Logger.Flush();
        }
        catch (Exception ex)
        {
            Console(ex.ToString());
        }

#if DEBUG
        if (System.Diagnostics.Debugger.IsAttached)
        {
            System.Diagnostics.Debugger.Break();
        }
#endif
    }

    private static void Console(string message)
    {
        System.Diagnostics.Debug.Print(message);
    }

    private static void AppendSystemInformation(StringBuilder sb)
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

        sb.Append("Version name: ");
        sb.Append(AppEnvironment.Instance.GetVersionName());
        sb.Append('\n');

        sb.Append("Crash time: ");
        sb.Append(DateTimeOffset.Now);
        sb.Append('\n');

        sb.Append("Launch time: ");
        sb.Append(AppEnvironment.Instance.GetLaunchTime());
        sb.Append('\n');

        sb.Append("Awake time: ");
        sb.Append(AppEnvironment.Instance.GetAwakeTime());
        sb.Append('\n');

        sb.Append("Installed system language: ");
        sb.Append(CultureInfo.InstalledUICulture.Name);
        sb.Append('\n');

        sb.Append("Current system language: ");
        sb.Append(CultureInfo.CurrentCulture.Name);
        sb.Append('\n');
    }
}
