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

        IRandomAccessStream sourceStream = null;

        if (cacheStream == null)
        {
            sourceStream = TryOpenImageStream(source);

            if (sourceStream == null)
            {
                Logger.F(TAG, "sourceStream is null");
                return;
            }

            try
            {
                cacheStream = TryCreateImageCache(cacheRecord, sourceStream, frameWidth, frameHeight, stretchMode, cacheKey, sourceSignature);
            }
            catch (Exception)
            {
                sourceStream.Dispose();
                throw;
            }
        }

        _ = MainThreadUtils.RunInMainThreadAsync(async delegate
        {
            BitmapImage image = null;
            try
            {
                if (token.IsCancellationRequested)
                {
                    return;
                }

                if (cacheStream != null)
                {
                    image = await TryLoadImageFromStream(cacheStream);
                }

                if (image == null)
                {
                    sourceStream ??= TryOpenImageStream(source);

                    if (sourceStream != null)
                    {
                        image = await TryLoadImageFromStream(sourceStream);
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
                    image.DecodePixelWidth = desiredWidth;
                    image.DecodePixelHeight = desiredHeight;
                }
            }
            finally
            {
                cacheStream?.Dispose();
                sourceStream?.Dispose();
            }

            handler.OnSuccess(image);
        });
    }

    private static IRandomAccessStream TryCreateImageCache(ImageCacheDatabase.CacheRecord cacheRecord, IRandomAccessStream sourceStream,
        double frameWidth, double frameHeight, StretchModeEnum stretchMode, string cacheKey, int sourceSignature)
    {
        sourceStream.Seek(0);
        BitmapDecoder decoder = null;
        try
        {
            decoder = BitmapDecoder.CreateAsync(sourceStream).AsTask().Result;
        }
        catch (Exception ex)
        {
            Logger.F(TAG, "TryCreateImageCache", ex);
        }
        if (decoder == null)
        {
            return null;
        }

        int sourceWidth = (int)decoder.PixelWidth;
        int sourceHeight = (int)decoder.PixelHeight;
        double aspectRatio = (double)sourceWidth / sourceHeight;

        if (!CalculateDesiredDimension(frameWidth, frameHeight, stretchMode, aspectRatio, out int desiredWidth, out int desiredHeight))
        {
            return null;
        }

        int desiredResolution = desiredHeight * desiredWidth;
        string cacheEntryKey = CalculateCacheEntryKey(desiredResolution);
        IRandomAccessStream cacheStream = CreateImageCacheStream(cacheEntryKey, sourceWidth, sourceHeight, decoder).Result;

        try
        {
            string entry = null;
            if (cacheStream != null)
            {
                try
                {
                    cacheStream.Seek(0);
                    byte[] outByteArray = new byte[cacheStream.Size];
                    cacheStream.ReadAsync(outByteArray.AsBuffer(), (uint)cacheStream.Size, InputStreamOptions.None).Wait();
                    string tempEntry = StringUtils.RandomFileName(16) + ".png";
                    using ILRUInputStream cacheFileStream = sImageCache.Value.Put(tempEntry);
                    if (cacheFileStream == null)
                    {
                        Logger.F(TAG, "TryCreateImageCache cacheFileStream is null");
                    }
                    else
                    {
                        cacheFileStream.WriteAsync(outByteArray.AsBuffer()).Wait();
                    }
                    entry = tempEntry;
                }
                catch (Exception e)
                {
                    Logger.F(TAG, "TryCreateImageCache", e);
                }
            }

            if (cacheRecord != null)
            {
                cacheRecord.UpdateMeta(sourceSignature, sourceWidth, sourceHeight);
            }
            else
            {
                cacheRecord = new ImageCacheDatabase.CacheRecord(cacheKey, sourceSignature, sourceWidth, sourceHeight);
            }

            if (entry != null)
            {
                cacheRecord.PutEntry(cacheEntryKey, entry);
            }

            cacheRecord.Save();
        }
        catch (Exception)
        {
            cacheStream?.Dispose();
            throw;
        }

        return cacheStream;
    }

    private static async Task<IRandomAccessStream> CreateImageCacheStream(string cacheEntryKey, int sourceWidth, int sourceHeight, BitmapDecoder decoder)
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

        int sourceResolution = sourceWidth * sourceHeight;
        if (sourceResolution <= cacheResolution)
        {
            return null;
        }

        double scaleRatio = (double)cacheResolution / sourceResolution;
        double dimensionRatio = Math.Sqrt(scaleRatio);
        uint aspectHeight = (uint)Math.Floor(sourceHeight * dimensionRatio);
        uint aspectWidth = (uint)Math.Floor(sourceWidth * dimensionRatio);

        InMemoryRandomAccessStream cacheStream = null;
        try
        {
            using SoftwareBitmap softwareBitmap = await decoder.GetSoftwareBitmapAsync();
            cacheStream = new InMemoryRandomAccessStream();
            BitmapEncoder encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, cacheStream);
            encoder.SetSoftwareBitmap(softwareBitmap);
            encoder.BitmapTransform.InterpolationMode = BitmapInterpolationMode.Fant;
            encoder.BitmapTransform.ScaledHeight = aspectHeight;
            encoder.BitmapTransform.ScaledWidth = aspectWidth;
            await encoder.FlushAsync();
        }
        catch (Exception e)
        {
            Logger.F(TAG, "CreateImageCacheStream", e);
            cacheStream?.Dispose();
            cacheStream = null;
        }
        return cacheStream;
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

    private static IRandomAccessStream TryOpenImageStream(IImageSource source)
    {
        try
        {
            return source.GetImageStream().Result;
        }
        catch (Exception e)
        {
            Logger.E(TAG, "TryLoadImageFromFile", e);
        }
        return null;
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
