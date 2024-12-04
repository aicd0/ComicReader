// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;

using ComicReader.Common.Caching;
using ComicReader.Common.DebugTools;
using ComicReader.Common.Threading;

using Microsoft.UI.Xaml.Media.Imaging;

using Windows.Graphics.Imaging;
using Windows.Storage;
using Windows.Storage.Streams;

namespace ComicReader.Common.Imaging;

internal static class ImageCacheManager
{
    private const string TAG = "ImageCacheManager";
    private const string CACHE_FOLDER = "images";
    private const long MAX_CACHE_SIZE = 1024 * 1024 * 1024;

    private const string CACHE_ENTRY_KEY_SMALL = "100k";
    private const int CACHE_ENTRY_RESOLUTION_SMALL = 100000;

    private static readonly Lazy<LRUCache> sImageCache = new(delegate
    {
        StorageFolder cacheFolder = ApplicationData.Current.LocalCacheFolder;
        StorageFolder imageFolder = cacheFolder.CreateFolderAsync(CACHE_FOLDER, CreationCollisionOption.OpenIfExists).AsTask().Result;
        LRUCache cache = new(imageFolder, MAX_CACHE_SIZE);
        TaskDispatcher.LongRunningThreadPool.Submit("CleanImageCache", delegate
        {
            cache.Clean();
        });
        return cache;
    });

    public static void LoadImage(CancellationSession.IToken token,
        IImageSource source, double frameWidth, double frameHeight, StretchModeEnum stretchMode,
        IImageResultHandler handler)
    {
        DebugUtils.Assert(!MainThreadUtils.IsMainThread());

        if (token.IsCancellationRequested)
        {
            return;
        }

        string cacheKey = source.GetCacheKey();
        int sourceSignature = source.GetContentSignature();

        IRandomAccessStream cacheStream = null;
        ImageCacheDatabase.CacheRecord cacheRecord = null;
        if (cacheKey != null)
        {
            cacheRecord = ImageCacheDatabase.GetCacheRecord(cacheKey);
            if (cacheRecord != null && (sourceSignature == 0 || cacheRecord.Signature == sourceSignature))
            {
                double aspectRatio = (double)cacheRecord.Width / cacheRecord.Height;
                if (CalculateDesiredDimension(frameWidth, frameHeight, stretchMode, aspectRatio, out int desiredWidth, out int desiredHeight))
                {
                    string cacheEntryKey = CalculateCacheEntryKey(desiredWidth * desiredHeight);
                    string entry = cacheRecord.GetEntry(cacheEntryKey);
                    if (entry.Length > 0)
                    {
                        cacheStream = sImageCache.Value.Get(entry);
                    }
                }
            }
        }

        BitmapImage image = null;
        int cacheDesiredResolution = 0;
        int cacheSourceWidth = 0;
        int cacheSourceHeight = 0;

        MainThreadUtils.RunInMainThreadAsync(async delegate
        {
            if (token.IsCancellationRequested)
            {
                return;
            }

            if (cacheStream != null)
            {
                image = await TryLoadImageFromStream(cacheStream);
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
                Logger.F(TAG, "image is null");
                return;
            }

            double aspectRatio = (double)image.PixelWidth / image.PixelHeight;
            if (CalculateDesiredDimension(frameWidth, frameHeight, stretchMode, aspectRatio, out int desiredWidth, out int desiredHeight))
            {
                cacheDesiredResolution = desiredWidth * desiredHeight;
                image.DecodePixelWidth = desiredWidth;
                image.DecodePixelHeight = desiredHeight;
            }

            if (isFromSource)
            {
                cacheSourceHeight = image.PixelHeight;
                cacheSourceWidth = image.PixelWidth;
            }
        }).Wait();

        cacheStream?.Dispose();

        if (image == null)
        {
            return;
        }

        if (cacheKey != null && cacheSourceHeight > 0 && cacheSourceWidth > 0)
        {
            if (cacheRecord != null)
            {
                cacheRecord.UpdateMeta(sourceSignature, cacheSourceWidth, cacheSourceHeight);
            }
            else
            {
                cacheRecord = new ImageCacheDatabase.CacheRecord(cacheKey, sourceSignature, cacheSourceWidth, cacheSourceHeight);
            }

            if (cacheDesiredResolution > 0)
            {
                string cacheEntryKey = CalculateCacheEntryKey(cacheDesiredResolution);
                string entry = CreateImageCache(source, cacheEntryKey).Result;
                if (entry != null)
                {
                    cacheRecord.PutEntry(cacheEntryKey, entry);
                }
            }

            cacheRecord.Save();
        }

        _ = MainThreadUtils.RunInMainThread(delegate
        {
            if (token.IsCancellationRequested)
            {
                return;
            }

            handler.OnSuccess(image);
        });
    }

    private static async Task<string> CreateImageCache(IImageSource source, string cacheEntryKey)
    {
        int cacheResolution;
        switch (cacheEntryKey)
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
        using (stream)
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
                using SoftwareBitmap softwareBitmap = await decoder.GetSoftwareBitmapAsync();
                var resizedStream = new InMemoryRandomAccessStream();
                BitmapEncoder encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, resizedStream);
                encoder.SetSoftwareBitmap(softwareBitmap);
                encoder.BitmapTransform.InterpolationMode = BitmapInterpolationMode.Fant;
                encoder.BitmapTransform.ScaledHeight = aspectHeight;
                encoder.BitmapTransform.ScaledWidth = aspectWidth;
                await encoder.FlushAsync();

                resizedStream.Seek(0);
                byte[] outByteArray = new byte[resizedStream.Size];
                await resizedStream.ReadAsync(outByteArray.AsBuffer(), (uint)resizedStream.Size, InputStreamOptions.None);

                entry = StringUtils.RandomFileName(16) + ".png";
                using ILRUInputStream cacheStream = sImageCache.Value.Put(entry);
                if (cacheStream == null)
                {
                    Logger.F(TAG, "CreateImageCache cacheStream is null");
                    return null;
                }
                await cacheStream.WriteAsync(outByteArray.AsBuffer());
            }
            catch (Exception e)
            {
                Logger.F(TAG, "CreateImageCache", e);
                return null;
            }
        }
        return entry;
    }

    private static string CalculateCacheEntryKey(int resolution)
    {
        string cacheEntryKey = "";

        if (resolution <= CACHE_ENTRY_RESOLUTION_SMALL)
        {
            cacheEntryKey = CACHE_ENTRY_KEY_SMALL;
        }

        return cacheEntryKey;
    }

    private static async Task<BitmapImage> TryLoadImageFromStream(IRandomAccessStream stream)
    {
        BitmapImage image = new();
        stream.Seek(0);
        try
        {
            await image.SetSourceAsync(stream);
        }
        catch (Exception e)
        {
            Logger.F(TAG, "TryLoadImageFromStream", e);
            image = null;
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
            Logger.F(TAG, "TryLoadImageFromSource", e);
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
                    Logger.F(TAG, "TryLoadImageFromSource", e);
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

        double rawPixelsPerViewPixel = DisplayUtils.GetRawPixelPerPixel();
        double frameRatio = frameWidth / frameHeight;
        double desiredWidthRaw;
        double desiredHeightRaw;
        if (imageRatio > frameRatio == (stretchMode == StretchModeEnum.Uniform))
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
}
