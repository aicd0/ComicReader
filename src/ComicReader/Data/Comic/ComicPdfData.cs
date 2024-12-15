// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Threading.Tasks;

using ComicReader.Common;
using ComicReader.Common.DebugTools;

using PdfiumViewer;

using Windows.Storage;
using Windows.Storage.Streams;

namespace ComicReader.Data.Comic;

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
        Storage.AddTrustedFile(file);

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

    protected override Task<TaskException> SaveToInfoFile()
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
        await LoadImageFiles();
        return new PdfComicConnection(ThisFile);
    }

    private class PdfComicConnection : IComicConnection
    {
        private readonly object _lock = new();
        private readonly StorageFile _pdfFile;
        private bool _opened = false;
        private PdfDocument _pdfDocument;

        public PdfComicConnection(StorageFile pdfFile)
        {
            _pdfFile = pdfFile;
        }

        public void Dispose()
        {
            _pdfDocument?.Dispose();
            _pdfDocument = null;
        }

        public int GetImageCount()
        {
            PdfDocument pdfDocument = OpenDocument();
            if (pdfDocument == null)
            {
                return 0;
            }
            return pdfDocument.PageCount;
        }

#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously
        public async Task<IRandomAccessStream> GetImageStream(int index)
#pragma warning restore CS1998 // Async method lacks 'await' operators and will run synchronously
        {
            PdfDocument pdfDocument = OpenDocument();
            if (pdfDocument == null)
            {
                return null;
            }

            MemoryStream memoryStream = new();
            try
            {
                Image image = pdfDocument.Render(index, 1, 1, false);
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

        private PdfDocument OpenDocument()
        {
            if (_opened)
            {
                return _pdfDocument;
            }

            lock (_lock)
            {
                if (_opened)
                {
                    return _pdfDocument;
                }

                _opened = true;
                try
                {
                    _pdfDocument = PdfDocument.Load(_pdfFile.Path);
                }
                catch (Exception ex)
                {
                    Logger.F(TAG, "OpenDocument", ex);
                    return null;
                }
                return _pdfDocument;
            }
        }
    }
}
