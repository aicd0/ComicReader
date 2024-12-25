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

    private class AutoDisposeHolder<T>(T instance) : IDisposable where T : class, IDisposable
    {
        private readonly object _lock = new();
        private int useCount = 0;
        private bool _disposeRequested = false;
        private bool _disposed = false;

        public void Dispose()
        {
            lock (_lock)
            {
                if (useCount <= 0)
                {
                    PerformDispose();
                }
                else
                {
                    _disposeRequested = true;
                }
            }
        }

        public T Acquire()
        {
            if (_disposed || _disposeRequested)
            {
                return null;
            }

            lock (_lock)
            {
                if (_disposed || _disposeRequested)
                {
                    return null;
                }

                useCount++;
                return instance;
            }
        }

        public void Release()
        {
            lock (_lock)
            {
                ArgumentOutOfRangeException.ThrowIfLessThan(useCount, 1);
                useCount--;

                if (_disposeRequested)
                {
                    PerformDispose();
                }
            }
        }

        private void PerformDispose()
        {
            if (!_disposed)
            {
                instance?.Dispose();
                instance = null;
                _disposed = true;
            }
        }
    }

    private class PdfComicConnection : IComicConnection
    {
        private readonly object _lock = new();
        private readonly StorageFile _pdfFile;
        private bool _openAttempted = false;
        private AutoDisposeHolder<PdfDocument> _pdfDocumentHolder;

        public PdfComicConnection(StorageFile pdfFile)
        {
            _pdfFile = pdfFile;
        }

        public void Dispose()
        {
            lock (_lock)
            {
                _pdfDocumentHolder?.Dispose();
                _pdfDocumentHolder = null;
                _openAttempted = true;
            }
        }

        public int GetImageCount()
        {
            return UsingDocument(delegate (PdfDocument pdfDocument)
            {
                return pdfDocument.PageCount;
            }, 0);
        }

#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously
        public async Task<IRandomAccessStream> GetImageStream(int index)
#pragma warning restore CS1998 // Async method lacks 'await' operators and will run synchronously
        {
            return UsingDocument(delegate (PdfDocument pdfDocument)
            {
                MemoryStream memoryStream = new();
                try
                {
                    SizeF size = pdfDocument.PageSizes[index];
                    CalculatePageSize(size.Width, size.Height, out int width, out int height);
                    using Image image = pdfDocument.Render(index, width, height, 1, 1, false);
                    image.Save(memoryStream, ImageFormat.Png);
                }
                catch (Exception e)
                {
                    Logger.F(TAG, "GetImageStream", e);
                    memoryStream.Dispose();
                    memoryStream = null;
                }
                return memoryStream?.AsRandomAccessStream();
            }, null);
        }

        private R UsingDocument<R>(Func<PdfDocument, R> action, R defaultValue)
        {
            AutoDisposeHolder<PdfDocument> holder = OpenDocumentInternal();
            if (holder == null)
            {
                return defaultValue;
            }
            PdfDocument pdfDocument = holder.Acquire();
            if (pdfDocument == null)
            {
                return defaultValue;
            }
            try
            {
                return action(pdfDocument);
            }
            finally
            {
                holder.Release();
            }
        }

        private AutoDisposeHolder<PdfDocument> OpenDocumentInternal()
        {
            if (_openAttempted)
            {
                return _pdfDocumentHolder;
            }

            lock (_lock)
            {
                if (_openAttempted)
                {
                    return _pdfDocumentHolder;
                }
                _openAttempted = true;

                PdfDocument pdfDocument = null;
                try
                {
                    pdfDocument = PdfDocument.Load(_pdfFile.Path);
                }
                catch (Exception ex)
                {
                    Logger.F(TAG, "OpenDocument", ex);
                    return null;
                }
                if (pdfDocument != null)
                {
                    _pdfDocumentHolder = new(pdfDocument);
                }
                return _pdfDocumentHolder;
            }
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
