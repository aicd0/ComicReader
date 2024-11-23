// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Drawing;
using System.IO;

using ComicReader.Utils;

using Windows.Storage.Streams;

namespace ComicReader.Common.SimpleImageView;

internal static class ImageInfoManager
{
    private const string TAG = "ImageInfoManager";

    public static ImageInfo GetImageInfo(IImageSource source)
    {
        string key = source.GetUniqueKey();
        ImageCacheDatabase.CacheRecord record = ImageCacheDatabase.GetCacheRecord(key);
        if (record != null)
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

        int width = 0;
        int height = 0;
        if (stream != null)
        {
            using (stream)
            {
                stream.Seek(0);
                try
                {
                    using var image = Image.FromStream(stream.AsStream());
                    if (image == null)
                    {
                        return null;
                    }
                    width = image.Width;
                    height = image.Height;
                }
                catch (Exception e)
                {
                    Logger.E(TAG, "GetImageInfo", e);
                }
            }
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
