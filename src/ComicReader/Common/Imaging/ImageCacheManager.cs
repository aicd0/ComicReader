// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;

using ComicReader.Common.Caching;
using ComicReader.Common.DebugTools;
using ComicReader.Common.Threading;

using Microsoft.UI.Xaml.Media.Imaging;

using Windows.Storage;
using Windows.Storage.Streams;

namespace ComicReader.Common.Imaging;

internal static class ImageCacheManager
{
    private const string TAG = "ImageCacheManager";
    private const string CACHE_FOLDER = "images";
    private const long MAX_CACHE_SIZE = 1024 * 1024 * 1024;

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

        long startTime = DateTimeOffset.Now.ToUnixTimeMilliseconds();
        string uri = source.GetUri();
        int sourceSignature = source.GetContentSignature();

        if (uri == null || uri.Length == 0)
        {
            DebugUtils.Assert(false);
            return;
        }

        IRandomAccessStream cacheStream = null;
        ImageCacheDatabase.CacheRecord cacheRecord = ImageCacheDatabase.GetCacheRecord(source);
        bool requireCache = true;

        if (cacheRecord != null)
        {
            requireCache = false;
            CalculateDesiredDimension(frameWidth, frameHeight, stretchMode, cacheRecord.Width, cacheRecord.Height, out int desiredWidth, out int desiredHeight);
            IEnumerable<string> cacheEntryKeys = ImageCacheStrategy.CalculateCacheEntryKeys(desiredWidth, desiredHeight, cacheRecord.Width, cacheRecord.Height);
            foreach (string cacheEntryKey in cacheEntryKeys)
            {
                requireCache = true;
                string entry = cacheRecord.GetEntry(cacheEntryKey);
                if (entry.Length > 0)
                {
                    cacheStream = sImageCache.Value.Get(entry);
                }
                if (cacheStream != null)
                {
                    break;
                }
            }
        }

        IRandomAccessStream sourceStream = null;

        if (cacheStream == null && requireCache)
        {
            sourceStream = TryOpenImageStreamAsync(source).Result;

            if (sourceStream == null)
            {
                Logger.F(TAG, "sourceStream is null");
                return;
            }

            try
            {
                cacheStream = TryCreateImageCache(cacheRecord, sourceStream, frameWidth, frameHeight, stretchMode, uri, sourceSignature);
            }
            catch (Exception)
            {
                sourceStream.Dispose();
                throw;
            }
        }

        MainThreadUtils.RunInMainThreadAsync(async delegate
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
                    image = await TryLoadImageFromStreamAsync(cacheStream);
                }

                if (image == null)
                {
                    sourceStream ??= await TryOpenImageStreamAsync(source);

                    if (sourceStream != null)
                    {
                        image = await TryLoadImageFromStreamAsync(sourceStream);
                    }
                }

                if (image == null)
                {
                    Logger.F(TAG, "image is null");
                    return;
                }

                CalculateDesiredDimension(frameWidth, frameHeight, stretchMode, image.PixelWidth, image.PixelHeight, out int desiredWidth, out int desiredHeight);
                if (desiredWidth != image.PixelWidth || desiredHeight != image.PixelHeight)
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

            if (token.IsCancellationRequested)
            {
                Logger.I(TAG, $"task cancelled (time={DateTimeOffset.Now.ToUnixTimeMilliseconds() - startTime},uri={uri})");
                return;
            }

            handler.OnSuccess(image);
        }).Wait();
    }

    private static IRandomAccessStream TryCreateImageCache(ImageCacheDatabase.CacheRecord cacheRecord, IRandomAccessStream sourceStream,
        double frameWidth, double frameHeight, StretchModeEnum stretchMode, string cacheKey, int sourceSignature)
    {
        sourceStream.Seek(0);
        Image image = null;
        try
        {
            image = Image.FromStream(sourceStream.AsStream());
        }
        catch (Exception ex)
        {
            Logger.F(TAG, "TryCreateImageCache", ex);
        }
        if (image == null)
        {
            return null;
        }

        int sourceWidth = image.Width;
        int sourceHeight = image.Height;
        CalculateDesiredDimension(frameWidth, frameHeight, stretchMode, sourceWidth, sourceHeight, out int desiredWidth, out int desiredHeight);
        IEnumerable<string> cacheEntryKeys = ImageCacheStrategy.CalculateCacheEntryKeys(desiredWidth, desiredHeight, sourceWidth, sourceHeight);
        string cacheEntryKey = null;
        foreach (string key in cacheEntryKeys)
        {
            cacheEntryKey = key;
            break;
        }
        MemoryStream cacheStream = CreateImageCacheStream(cacheEntryKey, sourceWidth, sourceHeight, image);

        try
        {
            string entry = null;
            if (cacheStream != null)
            {
                try
                {
                    cacheStream.Seek(0, SeekOrigin.Begin);
                    byte[] outByteArray = new byte[cacheStream.Length];
                    cacheStream.Read(outByteArray, 0, outByteArray.Length);
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

        return cacheStream?.AsRandomAccessStream();
    }

    private static MemoryStream CreateImageCacheStream(string cacheEntryKey, int sourceWidth, int sourceHeight, Image image)
    {
        if (cacheEntryKey == null || cacheEntryKey.Length == 0)
        {
            return null;
        }

        int cacheResolution = ImageCacheStrategy.GetCacheResolution(cacheEntryKey);
        if (cacheResolution <= 0)
        {
            return null;
        }

        int sourceResolution = sourceWidth * sourceHeight;
        if (sourceResolution <= cacheResolution)
        {
            return null;
        }

        double scaleRatio = (double)cacheResolution / sourceResolution;
        double dimensionRatio = Math.Sqrt(scaleRatio);
        int aspectHeight = (int)Math.Floor(sourceHeight * dimensionRatio);
        int aspectWidth = (int)Math.Floor(sourceWidth * dimensionRatio);

        MemoryStream memoryStream = new();
        try
        {
            using Bitmap resizedBitmap = new(aspectWidth, aspectHeight);
            using var graphics = Graphics.FromImage(resizedBitmap);
            graphics.CompositingQuality = CompositingQuality.HighSpeed;
            graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
            graphics.CompositingMode = CompositingMode.SourceCopy;
            graphics.DrawImage(image, 0, 0, aspectWidth, aspectHeight);
            resizedBitmap.Save(memoryStream, ImageFormat.Png);
        }
        catch (Exception e)
        {
            Logger.F(TAG, "CreateImageCacheStream", e);
            memoryStream.Dispose();
            memoryStream = null;
        }

        return memoryStream;
    }

    private static async Task<IRandomAccessStream> TryOpenImageStreamAsync(IImageSource source)
    {
        try
        {
            return await source.GetImageStream();
        }
        catch (Exception e)
        {
            Logger.E(TAG, "TryLoadImageFromFile", e);
        }

        return null;
    }

    private static async Task<BitmapImage> TryLoadImageFromStreamAsync(IRandomAccessStream stream)
    {
        BitmapImage image = new();

        try
        {
            stream.Seek(0);
            await image.SetSourceAsync(stream);
        }
        catch (Exception e)
        {
            Logger.F(TAG, "TryLoadImageFromStream", e);
            image = null;
        }

        return image;
    }

    private static void CalculateDesiredDimension(double frameWidth, double frameHeight,
        StretchModeEnum stretchMode, int originWidth, int originHeight, out int desiredWidth, out int desiredHeight)
    {
        double rawPixelsPerViewPixel = DisplayUtils.GetRawPixelPerPixel();
        double imageRatio = (double)originWidth / originHeight;
        double frameRatio = frameWidth / frameHeight;
        double desiredWidthRaw;
        double desiredHeightRaw;
        if (imageRatio > frameRatio == (stretchMode == StretchModeEnum.Uniform))
        {
            if (double.IsInfinity(frameWidth))
            {
                desiredWidthRaw = originWidth;
                desiredHeightRaw = originHeight;
            }
            else
            {
                desiredWidthRaw = frameWidth * rawPixelsPerViewPixel;
                desiredHeightRaw = desiredWidthRaw / imageRatio;
            }
        }
        else
        {
            if (double.IsInfinity(frameHeight))
            {
                desiredWidthRaw = originWidth;
                desiredHeightRaw = originHeight;
            }
            else
            {
                desiredHeightRaw = frameHeight * rawPixelsPerViewPixel;
                desiredWidthRaw = desiredHeightRaw * imageRatio;
            }
        }

        desiredWidth = (int)desiredWidthRaw;
        desiredHeight = (int)desiredHeightRaw;
    }
}
