﻿// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System.Drawing;

using ComicReader.SDK.Common.DebugTools;
using ComicReader.SDK.Common.Threading;

using PdfiumViewer;

namespace ComicReader.SDK.Common.Pdf;

public static class PdfManager
{
    private const string TAG = nameof(PdfManager);

    private static readonly object _lock = new();
    private static readonly Lazy<IDisposableTaskDispatcher> _pdfQueue = new(() => TaskDispatcher.Factory.NewSingleThread("PdfQueue"));
    private static readonly Dictionary<string, PdfWrapper> _cache = [];

    public static Task<IPdfConnection?> OpenPdf(string filepath, string? password)
    {
        string fullpath;
        try
        {
            fullpath = Path.GetFullPath(filepath);
        }
        catch (Exception ex)
        {
            Logger.F(TAG, "", ex);
            return Task.FromResult<IPdfConnection?>(null);
        }

        lock (_lock)
        {
            if (_cache.TryGetValue(fullpath, out PdfWrapper? existing))
            {
                existing.UseCount++;
                IPdfConnection connection = new PdfConnection(existing);
                return Task.FromResult<IPdfConnection?>(connection);
            }
        }

        IPdfConnection? loadPdfFunc()
        {
            lock (_lock)
            {
                if (_cache.TryGetValue(fullpath, out PdfWrapper? existing))
                {
                    existing.UseCount++;
                    return new PdfConnection(existing);
                }
            }

            PdfDocument? pdfDocument = null;
            try
            {
                pdfDocument = PdfDocument.Load(fullpath, password);
            }
            catch (Exception ex)
            {
                Logger.F(TAG, "OpenDocument", ex);
                return null;
            }

            if (pdfDocument == null)
            {
                return null;
            }

            PdfWrapper wrapper = new(pdfDocument);
            lock (_lock)
            {
                _cache.Add(fullpath, wrapper);
            }
            Logger.I(TAG, $"Opened {fullpath}");
            return new PdfConnection(wrapper);
        }
        return Enqueue(loadPdfFunc, "LoadPdf");
    }

    private static Task<T> Enqueue<T>(Func<T> op, string taskName)
    {
        var taskResult = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        _pdfQueue.Value.Submit($"{TAG}#Enqueue#{taskName}", delegate
        {
            taskResult.SetResult(op());
        });
        return taskResult.Task;
    }

    private class PdfWrapper : IDisposable
    {
        public PdfDocument? RawPdfDocument;
        public int UseCount = 1;
        public readonly int PageCount;
        public readonly List<SizeF> PageSizes;

        public PdfWrapper(PdfDocument pdfDocument)
        {
            RawPdfDocument = pdfDocument;
            PageCount = pdfDocument.PageCount;
            PageSizes = [.. pdfDocument.PageSizes];
        }

        public void Dispose()
        {
            RawPdfDocument?.Dispose();
            RawPdfDocument = null;
        }
    }

    public interface IPdfConnection : IDisposable
    {
        int GetPageCount();

        SizeF GetPageSize(int page);

        Task<Image?> Render(int page, int width, int height);
    }

    private class PdfConnection : IPdfConnection
    {
        private readonly PdfWrapper _wrapper;
        private int _disposed = 0;

        public PdfConnection(PdfWrapper wrapper)
        {
            _wrapper = wrapper;
        }

        public void Dispose()
        {
            if (Interlocked.CompareExchange(ref _disposed, 1, 0) == 1)
            {
                return;
            }

            lock (_lock)
            {
                _wrapper.UseCount--;
            }

            _ = Enqueue(delegate
            {
                lock (_lock)
                {
                    List<string> keys = new(_cache.Keys);
                    foreach (string key in keys)
                    {
                        PdfWrapper cache = _cache[key];
                        if (cache.UseCount > 0)
                        {
                            continue;
                        }
                        cache.Dispose();
                        _cache.Remove(key);
                        Logger.I(TAG, $"Disposed {key}");
                    }
                }
                return true;
            }, "Clean");
        }

        public int GetPageCount()
        {
            return _wrapper.PageCount;
        }

        public SizeF GetPageSize(int page)
        {
            return _wrapper.PageSizes[page];
        }

        public Task<Image?> Render(int page, int width, int height)
        {
            return Enqueue(delegate
            {
                if (_wrapper.RawPdfDocument == null)
                {
                    return null;
                }

                return _wrapper.RawPdfDocument.Render(page, width, height, 1, 1, false);
            }, "Render");
        }
    }
}
