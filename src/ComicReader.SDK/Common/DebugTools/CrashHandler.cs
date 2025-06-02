﻿// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System.Text;

using ComicReader.SDK.Common.ServiceManagement;

using Windows.Storage;

namespace ComicReader.SDK.Common.DebugTools;

public static class CrashHandler
{
    public static void OnUnhandledException(Exception e)
    {
        try
        {
            StringBuilder sb = new();

            sb.Append("Message:\n");
            sb.Append(e.Message);
            sb.Append("\n\n");

            sb.Append("Stack trace:\n");
            sb.Append(e.ToString());
            sb.Append("\n\n");

            sb.Append("Crash time: ");
            sb.Append(DateTimeOffset.Now);
            sb.Append('\n');

            {
                IApplicationService? service = ServiceManager.GetServiceNullable<IApplicationService>();
                if (service != null)
                {
                    sb.Append(service.GetEnvironmentDebugInfo());
                }
            }

            string fileName = $"crash_report_{DateTimeOffset.Now:yyyyMMddHHmmss}_{RandomString(4)}.txt";
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

    private static string RandomString(int length)
    {
        const string symbols = "0123456789abcdefghijklmnopqrstuvwxyz";
        StringBuilder sb = new();
        for (int i = 0; i < length; ++i)
        {
            sb.Append(symbols[Random.Shared.Next(symbols.Length)]);
        }
        return sb.ToString();
    }

    private static void Console(string message)
    {
        System.Diagnostics.Debug.Print(message);
    }
}
