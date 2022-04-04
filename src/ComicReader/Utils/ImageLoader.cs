#if DEBUG
//#define DEBUG_LOG_LOAD
#endif

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Windows.Graphics.Display;
using Windows.Storage;
using Windows.Storage.Streams;
using Windows.UI.Xaml.Media.Imaging;
using ComicReader.Database;

namespace ComicReader.Utils
{
    using RawTask = Task<Utils.TaskResult>;
    using SealedTask = Func<Task<Utils.TaskResult>, Utils.TaskResult>;

    public enum ImageConstrainOption
    {
        None,
        SameAsFirstImage
    }

    public class ImageLoaderToken
    {
        public ComicData Comic;
        public int Index;
        public Action<BitmapImage> Callback;
        public Func<BitmapImage, Task> CallbackAsync;
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

    public class ImageLoader
    {
        private static void _Log(string text)
        {
            Utils.Debug.Log("Image Loader: " + text);
        }

        public static async RawTask Load(LockContext db,
            IEnumerable<ImageLoaderToken> tokens, ImageConstrain max_width,
            ImageConstrain max_height, Utils.CancellationLock cancellation_lock)
        {
            List<ImageLoaderToken> tokens_cpy = new List<ImageLoaderToken>(tokens);

#if DEBUG_LOG_LOAD
            _Log("Loading " + tokens_cpy.Count.ToString() + " images");
#endif

            bool use_origin_size =
                max_width.Option == ImageConstrainOption.None &&
                double.IsInfinity(max_width.Val) &&
                max_height.Option == ImageConstrainOption.None &&
                double.IsInfinity(max_height.Val);

            double raw_pixels_per_view_pixel = 0.0;
            double frame_ratio = 0.0;
            bool first_token = true;
            bool all_token_success = true;

            for (int token_processed = 0; tokens_cpy.Count > 0; ++token_processed)
            {
                if (cancellation_lock.CancellationRequested)
                {
#if DEBUG_LOG_LOAD
                    _Log("Task cancelled");
#endif
                    return new TaskResult(TaskException.Cancellation);
                }

                ImageLoaderToken token = tokens_cpy.First();
                tokens_cpy.RemoveAt(0);

                // Lazy load.
                ComicData comic = token.Comic;
                TaskResult r = await comic.UpdateImages(db, cover_only: token.Index < 0, reload: false);

                // Skip tokens whose comic folder cannot be reached.
                if (!r.Successful)
                {
                    _Log("Token " + token.Index.ToString() + "(" + token_processed.ToString() + ") skipped, failed to update comic images");
                    int token_before = tokens_cpy.Count;
                    tokens_cpy.RemoveAll(x => x.Comic == comic);
                    _Log((tokens_cpy.Count - token_before).ToString() + " tokens with the same comic were removed.");
                    all_token_success = false;
                    continue;
                }

                BitmapImage image = null;

                using (IRandomAccessStream stream = await comic.GetImageStream(db, token.Index))
                {
                    if (stream == null)
                    {
                        _Log("Failed to get img stream " + token.Index.ToString() + ", skipped");
                        all_token_success = false;
                        continue;
                    }

                    stream.Seek(0);
                    bool img_load_success = true;

                    // IMPORTANT: Use TaskCompletionSource to guarantee all async tasks
                    // in Sync block has completed.
                    TaskCompletionSource<bool> completion_src = new TaskCompletionSource<bool>();

                    await Utils.C0.Sync(async delegate
                    {
                        image = new BitmapImage();

                        try
                        {
                            await image.SetSourceAsync(stream).AsTask();
                        }
                        catch (Exception e)
                        {
                            img_load_success = false;
                            _Log("Skipped token " + token.Index.ToString() + ", image corrupted. " + e.ToString());
                        }

                        completion_src.SetResult(true);
                    });

                    await completion_src.Task;

                    if (!img_load_success)
                    {
                        continue;
                    }
                }

                {
                    TaskCompletionSource<bool> completion_src = new TaskCompletionSource<bool>();

                    await Utils.C0.Sync(async delegate
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

                        if (token.CallbackAsync != null)
                        {
                            await token.CallbackAsync(image);
                        }

#if DEBUG_LOG_LOAD
                        _Log("Token " + token_i.ToString() + " (idx=" + token.Index.ToString() + ") loaded");
#endif

                        completion_src.SetResult(true);
                    });

                    await completion_src.Task;
                }
            }

#if DEBUG_LOG_LOAD
            _Log("All tokens loaded (trig_update=" + trig_update.ToString() + ")");
#endif

            if (!all_token_success)
            {
                return new TaskResult(TaskException.Failure);
            }

            return new TaskResult();
        }
    }
}
