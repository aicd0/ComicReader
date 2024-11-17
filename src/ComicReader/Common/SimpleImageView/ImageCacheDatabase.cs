// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;

using ComicReader.Native;
using ComicReader.Utils;

using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml.Media.Imaging;

using Windows.Storage.Streams;

using WinRT.Interop;

namespace ComicReader.Common.SimpleImageView;

internal static class ImageCacheDatabase
{
    private static double s_rawPixelPerPixel = -1;

    public static BitmapImage GetImage(CancellationSession.SessionToken token, IImageSource source,
        double frameWidth, double frameHeight, StretchModeEnum stretchMode)
    {
        bool useOriginalSize = double.IsInfinity(frameWidth) && double.IsInfinity(frameHeight);
        double rawPixelsPerViewPixel = GetRawPixelPerPixel();
        double frameRatio = frameWidth / frameHeight;

        BitmapImage image = null;
        using (IRandomAccessStream stream = source.GetImageStream())
        {
            if (stream == null)
            {
                // failure
                return null;
            }

            stream.Seek(0);
            bool imgLoadSuccess = Threading.RunInMainThreadAsync(async delegate
            {
                if (token.IsCancellationRequested)
                {
                    // cancelled
                    return false;
                }

                image = new BitmapImage();
                try
                {
                    await image.SetSourceAsync(stream).AsTask();
                }
                catch (Exception)
                {
                    // failure
                    return false;
                }

                if (!useOriginalSize)
                {
                    double imageRatio = (double)image.PixelWidth / image.PixelHeight;
                    double imageWidth;
                    double imageHeight;
                    if ((imageRatio > frameRatio) == (stretchMode == StretchModeEnum.Uniform))
                    {
                        imageWidth = frameWidth * rawPixelsPerViewPixel;
                        imageHeight = imageWidth / imageRatio;
                    }
                    else
                    {
                        imageHeight = frameHeight * rawPixelsPerViewPixel;
                        imageWidth = imageHeight * imageRatio;
                    }

                    image.DecodePixelHeight = (int)imageHeight;
                    image.DecodePixelWidth = (int)imageWidth;
                }
                return true;
            }).Result;
            if (!imgLoadSuccess)
            {
                // failure
                return null;
            }
        }
        return image;
    }

    private static double GetRawPixelPerPixel()
    {
        if (s_rawPixelPerPixel < 0)
        {
            s_rawPixelPerPixel = GetScaleAdjustment();
        }

        return s_rawPixelPerPixel;
    }

    private static double GetScaleAdjustment()
    {
        IntPtr hWnd = WindowNative.GetWindowHandle(App.Window);
        WindowId wndId = Win32Interop.GetWindowIdFromWindow(hWnd);
        var displayArea = DisplayArea.GetFromWindowId(wndId, DisplayAreaFallback.Primary);
        IntPtr hMonitor = Win32Interop.GetMonitorFromDisplayId(displayArea.DisplayId);

        // Get DPI.
        int result = NativeMethods.GetDpiForMonitor(hMonitor, NativeModels.MonitorDPIType.MDT_Default, out uint dpiX, out uint _);
        if (result != 0)
        {
            throw new Exception("Could not get DPI for monitor.");
        }

        uint scaleFactorPercent = (uint)(((long)dpiX * 100 + (96 >> 1)) / 96);
        return scaleFactorPercent / 100.0;
    }
}
