// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Threading.Tasks;

using ComicReader.Common;
using ComicReader.Common.DebugTools;

using Windows.Data.Pdf;
using Windows.Storage;
using Windows.Storage.Streams;

namespace ComicReader.Database;

internal class ComicPdfData : ComicData
{
    private const string TAG = "ComicPdfData";
    private const int WrongPassword = unchecked((int)0x8007052b); // HRESULT_FROM_WIN32(ERROR_WRONG_PASSWORD)
    private const int GenericFail = unchecked((int)0x80004005);   // E_FAIL

    private StorageFile ThisFile = null;
    private PdfDocument ThisDocument = null;

    public override int ImageCount => ThisDocument == null ? 0 : (int)ThisDocument.PageCount;
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

        await comic.UpdateImages(reload: true);
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

    private async Task<TaskException> SetDocument()
    {
        if (ThisDocument != null)
        {
            return TaskException.Success;
        }

        TaskException r = await SetFile();
        if (!r.Successful())
        {
            return r;
        }

        try
        {
            ThisDocument = await PdfDocument.LoadFromFileAsync(ThisFile);
            // ThisDocument = await PdfDocument.LoadFromFileAsync(ThisFile, PasswordBox.Password);
        }
        catch (Exception ex)
        {
            return ex.HResult switch
            {
                WrongPassword => TaskException.IncorrectPassword,
                GenericFail => TaskException.FileCorrupted,
                _ => TaskException.Failure,
            };
        }

        if (ThisDocument == null)
        {
            return TaskException.Failure;
        }

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
        TaskException r = await SetDocument();
        if (!r.Successful())
        {
            return r;
        }

        return TaskException.Success;
    }

    protected override async Task<IRandomAccessStream> InternalGetImageStream(int index)
    {
        TaskException r = await SetDocument();
        if (!r.Successful())
        {
            return null;
        }

        if (index >= ImageCount)
        {
            Logger.F(TAG, "InternalGetImageStream");
            return null;
        }

        using (PdfPage page = ThisDocument.GetPage((uint)index))
        {
            var stream = new InMemoryRandomAccessStream();
            await page.RenderToStreamAsync(stream);
            return stream;
        }
    }

    public override string GetImageCacheKey(int index)
    {
        return ThisFile.Path + ":" + index.ToString();
    }
}
