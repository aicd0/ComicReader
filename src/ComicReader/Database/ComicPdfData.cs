using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Data.Pdf;
using Windows.Storage;
using Windows.Storage.Streams;

namespace ComicReader.Database
{
    using RawTask = Task<Utils.TaskResult>;
    using SealedTask = Func<Task<Utils.TaskResult>, Utils.TaskResult>;
    using TaskResult = Utils.TaskResult;
    using TaskException = Utils.TaskException;

    class ComicPdfData : ComicData
    {
        const int WrongPassword = unchecked((int)0x8007052b); // HRESULT_FROM_WIN32(ERROR_WRONG_PASSWORD)
        const int GenericFail = unchecked((int)0x80004005);   // E_FAIL

        private StorageFile ThisFile = null;
        private PdfDocument ThisDocument = null;

        public override int ImageCount => ThisDocument == null ? 0 : (int)ThisDocument.PageCount;
        public override bool IsEditable => !IsExternal;

        private ComicPdfData(bool is_external) :
            base(ComicType.PDF, is_external) { }

        public static ComicData FromDatabase(string location)
        {
            return new ComicPdfData(false)
            {
                Location = location,
            };
        }

        public static async Task<ComicData> FromExternal(LockContext db, StorageFile file)
        {
            Utils.Storage.AddTrustedFile(file);

            ComicPdfData comic = new ComicPdfData(true)
            {
                Title1 = file.DisplayName,
                Location = file.Path,
            };

            await comic.UpdateImages(db, cover_only: false, reload: true);
            return comic;
        }

        private async RawTask SetFile()
        {
            if (ThisFile != null)
            {
                return new TaskResult();
            }

            if (Location == null)
            {
                return new TaskResult(TaskException.InvalidParameters);
            }

            string base_path = Utils.ArchiveAccess.GetBasePath(Location);
            StorageFile file = await Utils.Storage.TryGetFile(base_path);

            if (file == null)
            {
                return new TaskResult(TaskException.NoPermission);
            }

            ThisFile = file;
            return new TaskResult();
        }

        private async RawTask SetDocument()
        {
            if (ThisDocument != null)
            {
                return new TaskResult();
            }

            TaskResult r = await SetFile();
            if (!r.Successful)
            {
                return r;
            }

            try
            {
                ThisDocument = await PdfDocument.LoadFromFileAsync(ThisFile);
                //ThisDocument = await PdfDocument.LoadFromFileAsync(ThisFile, PasswordBox.Password);
            }
            catch (Exception ex)
            {
                switch (ex.HResult)
                {
                    case WrongPassword:
                        return new TaskResult(TaskException.IncorrectPassword);

                    case GenericFail:
                        return new TaskResult(TaskException.FileCorrupted);

                    default:
                        return new TaskResult(TaskException.Failure);
                }
            }

            if (ThisDocument == null)
            {
                return new TaskResult(TaskException.Failure);
            }

            return new TaskResult();
        }

        public override RawTask LoadFromInfoFile()
        {
            return Task.FromResult(new TaskResult(TaskException.NotSupported));
        }

        protected override RawTask SaveToInfoFile()
        {
            return Task.FromResult(new TaskResult(TaskException.NotSupported));
        }

        protected override async RawTask ReloadImages(LockContext db, bool cover_only)
        {
            TaskResult r = await SetDocument();

            if (!r.Successful)
            {
                return r;
            }

            ImageUpdated = true;
            return new TaskResult();
        }

        public override async Task<IRandomAccessStream> GetImageStream(LockContext db, int index)
        {
            TaskResult r = await SetDocument();
            if (!r.Successful)
            {
                return null;
            }

            if (index >= ImageCount)
            {
                Log("Image index " + index.ToString() + " out of boundary " + ImageCount.ToString());
                return null;
            }

            using (PdfPage page = ThisDocument.GetPage((uint)index))
            {
                var stream = new InMemoryRandomAccessStream();
                await page.RenderToStreamAsync(stream);
                //switch (Options.SelectedIndex)
                //{
                //    // View actual size.
                //    case 0:
                //        await page.RenderToStreamAsync(stream);
                //        break;

                //    // View half size on beige background.
                //    case 1:
                //        var options1 = new PdfPageRenderOptions();
                //        options1.BackgroundColor = Windows.UI.Colors.Beige;
                //        options1.DestinationHeight = (uint)(page.Size.Height / 2);
                //        options1.DestinationWidth = (uint)(page.Size.Width / 2);
                //        await page.RenderToStreamAsync(stream, options1);
                //        break;

                //    // Crop to center.
                //    case 2:
                //        var options2 = new PdfPageRenderOptions();
                //        var rect = page.Dimensions.TrimBox;
                //        options2.SourceRect = new Rect(rect.X + rect.Width / 4, rect.Y + rect.Height / 4, rect.Width / 2, rect.Height / 2);
                //        await page.RenderToStreamAsync(stream, options2);
                //        break;
                //}
                return stream;
            }
        }
    }
}
