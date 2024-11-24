// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;

using ComicReader.Common;
using ComicReader.Common.DebugTools;
using ComicReader.Common.Threading;
using ComicReader.Native;

using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml.Media.Imaging;

using Windows.Graphics.Imaging;
using Windows.Storage;
using Windows.Storage.Streams;

using WinRT.Interop;

namespace ComicReader.Common.SimpleImageView;

internal class ImageCacheManager
{
    private const string TAG = "ImageCacheManager";
    private const string CACHE_ENTRY_KEY_SMALL = "100k";
    private const int CACHE_ENTRY_RESOLUTION_SMALL = 100000;
    private static double s_rawPixelPerPixel = -1;

    public static void LoadImage(CancellationSession.IToken token, IImageSource source,
        double frameWidth, double frameHeight, StretchModeEnum stretchMode, IImageResultHandler handler)
    {
        if (token.IsCancellationRequested)
        {
            // cancelled
            return;
        }

        StorageFile cacheFile = null;
        ImageCacheDatabase.CacheRecord cacheRecord = null;
        string uniqueKey = source.GetUniqueKey();
        if (uniqueKey != null)
        {
            cacheRecord = ImageCacheDatabase.GetCacheRecord(uniqueKey);
            if (cacheRecord != null)
            {
                double aspectRatio = (double)cacheRecord.Width / cacheRecord.Height;
                if (CalculateDesiredDimension(frameWidth, frameHeight, stretchMode, aspectRatio, out int desiredWidth, out int desiredHeight))
                {
                    string targetCacheEntryKey = CalculateTargetCacheEntry(desiredWidth * desiredHeight);
                    string entry = cacheRecord.GetEntry(targetCacheEntryKey);
                    if (entry.Length > 0)
                    {
                        IStorageItem item = ImageCacheDatabase.CacheFolder.TryGetItemAsync(entry).Get();
                        if (item is StorageFile)
                        {
                            cacheFile = item as StorageFile;
                        }
                    }
                }
            }
        }

        int desiredResolution = 0;
        int sourceWidth = 0;
        int sourceHeight = 0;

        BitmapImage image = null;
        MainThreadUtils.RunInMainThreadAsync(async delegate
        {
            if (token.IsCancellationRequested)
            {
                // cancelled
                return;
            }

            if (cacheFile != null)
            {
                image = await TryLoadImageFromFile(cacheFile);
            }

            bool isFromSource = false;
            if (image == null)
            {
                image = await TryLoadImageFromSource(source);
                if (image != null)
                {
                    isFromSource = true;
                }
            }

            if (image == null)
            {
                // failure
                return;
            }

            double aspectRatio = (double)image.PixelWidth / image.PixelHeight;
            if (CalculateDesiredDimension(frameWidth, frameHeight, stretchMode, aspectRatio, out int desiredWidth, out int desiredHeight))
            {
                desiredResolution = desiredWidth * desiredHeight;
                image.DecodePixelWidth = desiredWidth;
                image.DecodePixelHeight = desiredHeight;
            }

            if (isFromSource)
            {
                sourceHeight = image.PixelHeight;
                sourceWidth = image.PixelWidth;
            }
        }).Wait();

        if (image == null)
        {
            // failure
            return;
        }

        if (sourceHeight > 0 && sourceWidth > 0 && uniqueKey != null)
        {
            if (cacheRecord != null)
            {
                cacheRecord.UpdateDimension(sourceWidth, sourceHeight);
            }
            else
            {
                cacheRecord = new ImageCacheDatabase.CacheRecord(uniqueKey, sourceWidth, sourceHeight);
            }
            if (desiredResolution > 0)
            {
                string targetCacheEntryKey = CalculateTargetCacheEntry(desiredResolution);
                string entry = CreateImageCache(source, targetCacheEntryKey).Result;
                if (entry != null)
                {
                    cacheRecord.PutEntry(targetCacheEntryKey, entry);
                }
            }
            cacheRecord.Save();
        }

        _ = MainThreadUtils.RunInMainThread(delegate
        {
            if (token.IsCancellationRequested)
            {
                // cancelled
                return;
            }

            handler.OnSuccess(image);
        });
    }

    private static async Task<string> CreateImageCache(IImageSource source, string cacheEntry)
    {
        int cacheResolution;
        switch (cacheEntry)
        {
            case CACHE_ENTRY_KEY_SMALL:
                cacheResolution = CACHE_ENTRY_RESOLUTION_SMALL;
                break;
            default:
                return null;
        }

        IRandomAccessStream stream = null;
        try
        {
            stream = await source.GetImageStream();
        }
        catch (Exception e)
        {
            Logger.E(TAG, "TryLoadImageFromFile", e);
        }
        if (stream == null)
        {
            return null;
        }

        string entry = null;
        try
        {
            stream.Seek(0);
            BitmapDecoder decoder = await BitmapDecoder.CreateAsync(stream);
            uint sourceWidth = decoder.PixelWidth;
            uint sourceHeight = decoder.PixelHeight;
            uint sourceResolution = sourceWidth * sourceHeight;
            if (sourceResolution <= cacheResolution)
            {
                return null;
            }

            double scaleRatio = (double)cacheResolution / sourceResolution;
            double dimensionRatio = Math.Sqrt(scaleRatio);
            uint aspectHeight = (uint)Math.Floor(sourceHeight * dimensionRatio);
            uint aspectWidth = (uint)Math.Floor(sourceWidth * dimensionRatio);

            try
            {
                using (SoftwareBitmap softwareBitmap = await decoder.GetSoftwareBitmapAsync())
                {
                    var resized_stream = new InMemoryRandomAccessStream();
                    BitmapEncoder encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, resized_stream);
                    encoder.SetSoftwareBitmap(softwareBitmap);
                    encoder.BitmapTransform.InterpolationMode = BitmapInterpolationMode.Fant;
                    encoder.BitmapTransform.ScaledHeight = aspectHeight;
                    encoder.BitmapTransform.ScaledWidth = aspectWidth;

                    await encoder.FlushAsync();
                    resized_stream.Seek(0);
                    byte[] out_buffer = new byte[resized_stream.Size];
                    await resized_stream.ReadAsync(out_buffer.AsBuffer(), (uint)resized_stream.Size, InputStreamOptions.None);

                    entry = StringUtils.RandomFileName(16) + ".png";
                    string filename = entry;
                    StorageFile sample_file = await ImageCacheDatabase.CacheFolder.CreateFileAsync(filename, CreationCollisionOption.ReplaceExisting);
                    await FileIO.WriteBytesAsync(sample_file, out_buffer);
                }
            }
            catch (Exception e)
            {
                Logger.F(TAG, "GenCoverCache", e);
                return null;
            }
        }
        finally
        {
            stream.Dispose();
        }
        return entry;
    }

    private static string CalculateTargetCacheEntry(int resolution)
    {
        string targetCacheEntryKey = "";
        if (resolution <= CACHE_ENTRY_RESOLUTION_SMALL)
        {
            targetCacheEntryKey = CACHE_ENTRY_KEY_SMALL;
        }
        return targetCacheEntryKey;
    }

    private static async Task<BitmapImage> TryLoadImageFromFile(StorageFile file)
    {
        IRandomAccessStream stream = null;
        try
        {
            stream = await file.OpenAsync(FileAccessMode.Read);
        }
        catch (Exception e)
        {
            Logger.E(TAG, "TryLoadImageFromFile", e);
        }

        BitmapImage image = null;
        if (stream != null)
        {
            using (stream)
            {
                stream.Seek(0);
                image = new BitmapImage();
                try
                {
                    await image.SetSourceAsync(stream);
                }
                catch (Exception e)
                {
                    image = null;
                    Logger.E(TAG, "TryLoadImageFromFile", e);
                }
            }
        }
        return image;
    }

    private static async Task<BitmapImage> TryLoadImageFromSource(IImageSource source)
    {
        IRandomAccessStream stream = null;
        try
        {
            stream = await source.GetImageStream();
        }
        catch (Exception e)
        {
            Logger.E(TAG, "TryLoadImageFromSource", e);
        }

        BitmapImage image = null;
        if (stream != null)
        {
            using (stream)
            {
                stream.Seek(0);
                image = new BitmapImage();
                try
                {
                    await image.SetSourceAsync(stream);
                }
                catch (Exception e)
                {
                    image = null;
                    Logger.E(TAG, "TryLoadImageFromSource", e);
                }
            }
        }
        return image;
    }

    private static bool CalculateDesiredDimension(double frameWidth, double frameHeight,
        StretchModeEnum stretchMode, double imageRatio, out int desiredWidth, out int desiredHeight)
    {
        desiredWidth = 0;
        desiredHeight = 0;

        double rawPixelsPerViewPixel = GetRawPixelPerPixel();
        double frameRatio = frameWidth / frameHeight;
        double desiredWidthRaw;
        double desiredHeightRaw;
        if ((imageRatio > frameRatio) == (stretchMode == StretchModeEnum.Uniform))
        {
            if (double.IsInfinity(frameWidth))
            {
                return false;
            }
            desiredWidthRaw = frameWidth * rawPixelsPerViewPixel;
            desiredHeightRaw = desiredWidthRaw / imageRatio;
        }
        else
        {
            if (double.IsInfinity(frameHeight))
            {
                return false;
            }
            desiredHeightRaw = frameHeight * rawPixelsPerViewPixel;
            desiredWidthRaw = desiredHeightRaw * imageRatio;
        }

        desiredWidth = (int)desiredWidthRaw;
        desiredHeight = (int)desiredHeightRaw;
        return true;
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
