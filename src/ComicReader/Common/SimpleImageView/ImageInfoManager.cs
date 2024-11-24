// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;

using ComicReader.Common.DebugTools;

using Windows.Graphics.Imaging;
using Windows.Storage.Streams;

namespace ComicReader.Common.SimpleImageView;

internal static class ImageInfoManager
{
    private const string TAG = "ImageInfoManager";

    public static ImageInfo GetImageInfo(IImageSource source)
    {
        string key = source.GetUniqueKey();
        ImageCacheDatabase.CacheRecord record = ImageCacheDatabase.GetCacheRecord(key);
        if (record != null && record.Width > 0 && record.Height > 0)
        {
            return new ImageInfo(record.Width, record.Height);
        }

        IRandomAccessStream stream = null;
        try
        {
            stream = source.GetImageStream().Result;
        }
        catch (Exception e)
        {
            Logger.E(TAG, "GetImageInfo", e);
        }
        if (stream == null)
        {
            return null;
        }

        int width = 0;
        int height = 0;
        using (stream)
        {
            try
            {
                stream.Seek(0);
                BitmapDecoder decoder = BitmapDecoder.CreateAsync(stream).AsTask().Result;
                width = (int)decoder.PixelWidth;
                height = (int)decoder.PixelHeight;
            }
            catch (Exception e)
            {
                Logger.E(TAG, "GetImageInfo", e);
                return null;
            }
        }

        if (width <= 0 || height <= 0)
        {
            DebugUtils.Assert(false);
            return null;
        }

        record = new ImageCacheDatabase.CacheRecord(key, width, height);
        record.Save();

        return new ImageInfo(width, height);
    }

    public class ImageInfo(int width, int height)
    {
        private readonly int _width = width;
        private readonly int _height = height;

        public int Width => _width;
        public int Height => _height;
    }
}
