// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;
using System.IO;
using System.Text;

using ComicReader.Common.AppEnvironment;
using ComicReader.SDK.Common.DebugTools;

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

            sb.Append("Crash time: ");
            sb.Append(DateTimeOffset.Now);
            sb.Append('\n');

            EnvironmentProvider.Instance.AppendDebugText(sb);

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

        if (DebugUtils.DebugBuild && System.Diagnostics.Debugger.IsAttached)
        {
            System.Diagnostics.Debugger.Break();
        }
    }

    private static void Console(string message)
    {
        System.Diagnostics.Debug.Print(message);
    }
}
