// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Drawing;

using ComicReader.Common.DebugTools;
using ComicReader.Common.Native;

using Microsoft.UI;
using Microsoft.UI.Windowing;

namespace ComicReader.Common;

internal static class DisplayUtils
{
    private const string TAG = "DisplayUtils";

    private static double sRawPixelPerPixel = -1;

    public static void GetScreenSize(out int width, out int height)
    {
        using var graphics = Graphics.FromHwnd(IntPtr.Zero);
        IntPtr hdc = graphics.GetHdc();
        width = NativeMethods.GetDeviceCaps(hdc, 118);
        height = NativeMethods.GetDeviceCaps(hdc, 117);
    }

    public static double GetRawPixelPerPixel()
    {
        if (sRawPixelPerPixel < 0)
        {
            try
            {
                sRawPixelPerPixel = GetScaleAdjustment();
            }
            catch (Exception ex)
            {
                Logger.F(TAG, "GetRawPixelPerPixel", ex);
                return 1.0;
            }
        }

        return sRawPixelPerPixel;
    }

    private static double GetScaleAdjustment()
    {
        nint hWnd = App.WindowManager.GetAnyWindow().WindowHandle;
        WindowId windowId = Win32Interop.GetWindowIdFromWindow(hWnd);
        var displayArea = DisplayArea.GetFromWindowId(windowId, DisplayAreaFallback.Primary);
        nint hMonitor = Win32Interop.GetMonitorFromDisplayId(displayArea.DisplayId);

        int returnCode = NativeMethods.GetDpiForMonitor(hMonitor, NativeModels.MonitorDPIType.MDT_Default, out uint dpiX, out uint _);
        if (returnCode != 0)
        {
            Logger.AssertNotReachHere("9610A388C81E2FA4");
            throw new Exception("Could not get DPI for monitor.");
        }

        uint scaleFactorPercent = (uint)(((long)dpiX * 100 + (96 >> 1)) / 96);
        return scaleFactorPercent / 100.0;
    }
}
