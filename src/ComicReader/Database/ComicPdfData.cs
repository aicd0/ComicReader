using System;
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

    public class ComicPdfData : ComicData
    {
        private const int WrongPassword = unchecked((int)0x8007052b); // HRESULT_FROM_WIN32(ERROR_WRONG_PASSWORD)
        private const int GenericFail = unchecked((int)0x80004005);   // E_FAIL

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
                // ThisDocument = await PdfDocument.LoadFromFileAsync(ThisFile, PasswordBox.Password);
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

        protected override async RawTask ReloadImages(LockContext db)
        {
            TaskResult r = await SetDocument();
            if (!r.Successful) return r;
            return new TaskResult();
        }

        protected override async Task<IRandomAccessStream> InternalGetImageStream(int index)
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
                return stream;
            }
        }
    }
}
