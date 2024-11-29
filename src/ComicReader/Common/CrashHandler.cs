// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;
using System.IO;
using System.Text;

using Windows.Storage;

namespace ComicReader.Common;

internal static class CrashHandler
{
    public static void OnUnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs e)
    {
        try
        {
            string fileName = $"crash_report_{DateTimeOffset.Now:yyyyMMddHHmmss}.txt";
            string filePath = ApplicationData.Current.LocalCacheFolder.Path + "\\" + fileName;
            string errorInfo = e.Exception.ToString();
            using FileStream fs = new(filePath, FileMode.Append, FileAccess.Write);
            byte[] info = new UTF8Encoding(true).GetBytes(errorInfo);
            fs.Write(info, 0, info.Length);
        }
        catch (Exception exception)
        {
            System.Diagnostics.Debug.Print(exception.ToString());
        }

#if DEBUG
        if (System.Diagnostics.Debugger.IsAttached)
        {
            System.Diagnostics.Debugger.Break();
        }
#endif
    }
}
