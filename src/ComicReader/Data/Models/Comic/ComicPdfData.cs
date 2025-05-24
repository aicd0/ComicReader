// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

#nullable disable

using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Threading.Tasks;

using ComicReader.Common;
using ComicReader.Common.DebugTools;
using ComicReader.Common.Pdf;

using Windows.Storage;
using Windows.Storage.Streams;

namespace ComicReader.Data.Models.Comic;

internal class ComicPdfData : ComicData
{
    private const string TAG = "ComicPdfData";

    private StorageFile ThisFile = null;

    public override bool IsEditable => !IsExternal;

    private ComicPdfData(bool is_external) :
        base(ComicType.PDF, is_external)
    { }

    public static ComicData FromDatabase(string location)
    {
        return new ComicPdfData(false)
        {
            Location = location,
        };
    }

    public static async Task<ComicData> FromExternal(StorageFile file)
    {
        var comic = new ComicPdfData(true)
        {
            Title1 = file.DisplayName,
            Location = file.Path,
        };

        await comic.ReloadImageFiles();
        return comic;
    }

    private async Task<TaskException> SetFile()
    {
        if (ThisFile != null)
        {
            return TaskException.Success;
        }

        if (Location == null)
        {
            return TaskException.InvalidParameters;
        }

        string base_path = ArchiveAccess.GetBasePath(Location, false);
        StorageFile file = await Storage.TryGetFile(base_path);

        if (file == null)
        {
            return TaskException.NoPermission;
        }

        ThisFile = file;
        return TaskException.Success;
    }

    public override Task<TaskException> LoadFromInfoFile()
    {
        return Task.FromResult(TaskException.NotSupported);
    }

    public override Task<TaskException> SaveToInfoFile()
    {
        return Task.FromResult(TaskException.NotSupported);
    }

    protected override async Task<TaskException> ReloadImages()
    {
        TaskException r = await SetFile();
        if (!r.Successful())
        {
            return r;
        }

        return TaskException.Success;
    }

    public override string GetImageCacheKey(int index)
    {
        return ThisFile.Path + ":" + index.ToString();
    }

    public override int GetImageSignature(int index)
    {
        return FileUtils.GetFileHashCode(ThisFile);
    }

    public override async Task<IComicConnection> OpenComicAsync()
    {
        TaskException r = await SetFile();
        if (!r.Successful())
        {
            return null;
        }

        PdfManager.IPdfConnection connection = await PdfManager.OpenPdf(ThisFile.Path);
        if (connection == null)
        {
            return null;
        }

        return new PdfComicConnection(connection);
    }

    private class PdfComicConnection : IComicConnection
    {
        private readonly PdfManager.IPdfConnection _connection;

        public PdfComicConnection(PdfManager.IPdfConnection connection)
        {
            _connection = connection;
        }

        public void Dispose()
        {
            _connection.Dispose();
        }

        public int GetImageCount()
        {
            return _connection.GetPageCount();
        }

        public async Task<IRandomAccessStream> GetImageStream(int index)
        {
            MemoryStream memoryStream = new();
            try
            {
                SizeF size = _connection.GetPageSize(index);
                CalculatePageSize(size.Width, size.Height, out int width, out int height);
                using Image image = await _connection.Render(index, width, height);
                if (image == null)
                {
                    return null;
                }
                image.Save(memoryStream, ImageFormat.Png);
            }
            catch (Exception e)
            {
                Logger.F(TAG, "GetImageStream", e);
                memoryStream.Dispose();
                memoryStream = null;
            }
            return memoryStream?.AsRandomAccessStream();
        }

        private void CalculatePageSize(float originWidth, float originHeight, out int width, out int height)
        {
            int defaultWidth = 764;
            int defaultHeight = 1080;

            if (!(float.IsFinite(originWidth) && float.IsFinite(originHeight) && originWidth > 0 && originHeight > 0))
            {
                width = defaultWidth;
                height = defaultHeight;
                return;
            }

            DisplayUtils.GetScreenSize(out int screenWidth, out int screenHeight);
            if (screenWidth <= 0 || screenHeight <= 0)
            {
                width = (int)originWidth;
                height = (int)originHeight;
                return;
            }

            float pageAspectRatio = originWidth / originHeight;
            float screenAspectRatio = (float)screenWidth / screenHeight;

            float targetWidth, targetHeight;
            if (pageAspectRatio > screenAspectRatio)
            {
                targetHeight = screenHeight;
                targetWidth = screenHeight / originHeight * originWidth;
            }
            else
            {
                targetWidth = screenWidth;
                targetHeight = screenWidth / originWidth * originHeight;
            }

            float maxResolution = 10000000;
            float targetResolution = targetWidth * targetHeight;
            if (targetResolution > maxResolution)
            {
                float dimensionFactor = (float)Math.Sqrt(maxResolution / targetResolution);
                targetWidth *= dimensionFactor;
                targetHeight *= dimensionFactor;
            }
            width = (int)targetWidth;
            height = (int)targetHeight;
        }
    }
}
