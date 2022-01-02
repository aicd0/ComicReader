using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Windows.Graphics.Display;
using Windows.Storage;
using Windows.Storage.Streams;
using Windows.UI.Xaml.Media.Imaging;
using ComicReader.Data;

namespace ComicReader.Utils
{
    using TaskResult = Utils.TaskQueue.TaskResult;

    public class ImageLoaderToken
    {
        public ComicData Comic;
        public int Index;
        public Action<BitmapImage> Callback;
    }

    public class ImageConstrain
    {
        public double Val;
        public ImageConstrainOption Option;

        public static implicit operator ImageConstrain(double val)
        {
            System.Diagnostics.Debug.Assert(!double.IsNaN(val));

            return new ImageConstrain
            {
                Val = val,
                Option = ImageConstrainOption.None
            };
        }

        public static implicit operator ImageConstrain(ImageConstrainOption opt)
        {
            return new ImageConstrain
            {
                Val = 0.0,
                Option = opt
            };
        }
    }

    public enum ImageConstrainOption
    {
        None,
        SameAsFirstImage
    }

    public class ImageLoader
    {
        public static async Task Load(LockContext db,
            IEnumerable<ImageLoaderToken> tokens, ImageConstrain max_width,
            ImageConstrain max_height, Utils.CancellationLock cancellation_lock)
        {
            List<ImageLoaderToken> tokens_cpy = new List<ImageLoaderToken>(tokens);

            bool use_origin_size =
                max_width.Option == ImageConstrainOption.None &&
                double.IsInfinity(max_width.Val) &&
                max_height.Option == ImageConstrainOption.None &&
                double.IsInfinity(max_height.Val);

            double raw_pixels_per_view_pixel = 0.0;
            double frame_ratio = 0.0;
            bool first_token = true;
            bool trig_update = false;

            for (; tokens_cpy.Count > 0;)
            {
                if (cancellation_lock.CancellationRequested)
                {
                    return;
                }

                ImageLoaderToken token = tokens_cpy.First();
                tokens_cpy.RemoveAt(0);

                // Lazy load.
                ComicData comic = token.Comic;

                if (comic.ImageFiles.Count <= token.Index)
                {
                    TaskResult r = await ComicDataManager.UpdateImages(
                        db, comic, cover: token.Index == 0);

                    // Skip tokens whose comic folder cannot be reached.
                    if (!r.Successful)
                    {
                        tokens_cpy.RemoveAll(x => x.Comic == comic);
                        trig_update = true;
                        continue;
                    }

                    if (comic.ImageFiles.Count <= token.Index)
                    {
                        trig_update = true;
                        continue;
                    }
                }

                StorageFile image_file = comic.ImageFiles[token.Index];
                IRandomAccessStream stream;

                try
                {
                    stream = await image_file.OpenAsync(FileAccessMode.Read);
                }
                catch (FileNotFoundException)
                {
                    trig_update = true;
                    continue;
                }

                BitmapImage image = null;
                Task task = null;

                await Utils.Methods.Sync(delegate
                {
                    image = new BitmapImage();
                    task = image.SetSourceAsync(stream).AsTask();
                });

                await task.AsAsyncAction();
                stream.Dispose();

                await Utils.Methods.Sync(delegate
                {
                    if (first_token)
                    {
                        first_token = false;

                        if (max_width.Option == ImageConstrainOption.SameAsFirstImage)
                        {
                            max_width.Val = image.PixelWidth;
                        }

                        if (max_height.Option == ImageConstrainOption.SameAsFirstImage)
                        {
                            max_height.Val = image.PixelHeight;
                        }

                        raw_pixels_per_view_pixel = DisplayInformation.GetForCurrentView().RawPixelsPerViewPixel;
                        frame_ratio = max_width.Val / max_height.Val;
                    }

                    if (!use_origin_size)
                    {
                        double image_ratio = (double)image.PixelWidth / image.PixelHeight;
                        double image_height;
                        double image_width;
                        if (image_ratio > frame_ratio)
                        {
                            image_width = max_width.Val * raw_pixels_per_view_pixel;
                            image_height = image_width / image_ratio;
                        }
                        else
                        {
                            image_height = max_height.Val * raw_pixels_per_view_pixel;
                            image_width = image_height * image_ratio;
                        }
                        image.DecodePixelHeight = (int)image_height;
                        image.DecodePixelWidth = (int)image_width;
                    }

                    token.Callback?.Invoke(image);
#if DEBUG_LOG_IMAGE_LOADED
                    System.Diagnostics.Debug.Print("Image " + token.Index.ToString() + " loaded.\n");
#endif
                });
            }

            // Not all the images are successfully loaded, most likely that some of the
            // files or directories has been renamed, moved or deleted. We trigger a
            // DatabaseManager.Update() here to sync the changes.
            if (trig_update)
            {
                Utils.TaskQueue.TaskQueueManager.AppendTask(DatabaseManager.UpdateSealed(), "",
                    Utils.TaskQueue.TaskQueueManager.EmptyQueue());
            }
        }
    }
}
