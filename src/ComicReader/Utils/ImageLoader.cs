#if DEBUG
//#define DEBUG_LOG_LOAD
//#define DEBUG_LOG_QUEUE
#endif

using ComicReader.Database;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Windows.Graphics.Display;
using Windows.Storage.Streams;
using Windows.UI.Xaml.Media.Imaging;

namespace ComicReader.Utils
{
    using SealedTask = Func<Task<TaskException>, TaskException>;

    internal static class ImageLoader
    {
        private static readonly TaskQueue s_loadQueue = new TaskQueue();
        private static double s_rawPixelPerPixel = -1;

        private static SealedTask LoadSingleImageSealed(
            Token token,
            double width, double height,
            StretchModeEnum stretch_mode
        ) {
            return (Task<TaskException> _) => LoadSingleImage(token, width, height, stretch_mode).Result;
        }

        public enum StretchModeEnum
        {
            Uniform,
            UniformToFill,
        }

        public class Token
        {
            public CancellationSession.Token SessionToken;
            public ComicData Comic;
            public int Index;
            public ILoadImageCallback Callback;
        }

        public sealed class Builder : BuilderBase<TaskException>
        {
            private readonly List<Token> _tokens;
            private double _width = double.PositiveInfinity;
            private double _height = double.PositiveInfinity;
            private StretchModeEnum _stretchMode = StretchModeEnum.Uniform;
            private double _multiplication = 1.0;

            public Builder(List<Token> tokens)
            {
                _tokens = tokens;
            }

            public Builder WidthConstrain(double value)
            {
                _width = value;
                return this;
            }

            public Builder HeightConstrain(double value)
            {
                _height = value;
                return this;
            }

            public Builder StretchMode(StretchModeEnum mode)
            {
                _stretchMode = mode;
                return this;
            }

            public Builder Multiplication(double value)
            {
                _multiplication = value;
                return this;
            }

            protected override TaskException CommitImpl()
            {
                _width *= _multiplication;
                _height *= _multiplication;
                foreach (Token token in _tokens)
                {
                    s_loadQueue.Enqueue(LoadSingleImageSealed(token, _width, _height, _stretchMode));
                }
#if DEBUG_LOG_QUEUE
                Log("Enqueued: " + s_loadQueue.PendingTaskCount.ToString());
#endif
                return TaskException.Success;
            }
        }

        private static async Task<double> GetRawPixelPerPixel()
        {
            if (s_rawPixelPerPixel < 0)
            {
                await C0.Sync(delegate
                {
                    s_rawPixelPerPixel = DisplayInformation.GetForCurrentView().RawPixelsPerViewPixel;
                });
            }
            return s_rawPixelPerPixel;
        }

        private static async Task<TaskException> LoadSingleImage(
            Token token,
            double width, double height,
            StretchModeEnum stretch_mode
        ) {
            if (token.SessionToken.Cancelled)
            {
#if DEBUG_LOG_LOAD
                Log("Task cancelled");
#endif
                return TaskException.Cancellation;
            }

#if DEBUG_LOG_LOAD
            Log("Loading " + tokens_cpy.Count.ToString() + " images");
#endif
            bool use_origin_size = double.IsInfinity(width) && double.IsInfinity(height);
            double raw_pixels_per_view_pixel = await GetRawPixelPerPixel();
            double frame_ratio = width / height;

            ComicData comic = token.Comic;
            TaskException result = await comic.UpdateImages(cover_only: token.Index < 0, reload: false);
            if (!result.Successful())
            {
                // Skip tokens whose comic folder cannot be reached.
                return TaskException.Failure;
            }

            BitmapImage image = null;
            using (IRandomAccessStream stream = await comic.GetImageStream(token.Index))
            {
                if (stream == null)
                {
                    Log("Failed to get img stream " + token.Index.ToString() + ", skipped");
                    return TaskException.Failure;
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
                        Log("Skipped token " + token.Index.ToString() + ", image corrupted. " + e.ToString());
                    }
                    completion_src.SetResult(true);
                });
                await completion_src.Task;
                if (!img_load_success)
                {
                    return TaskException.Failure;
                }
            }

            TaskCompletionSource<TaskException> taskResult = new TaskCompletionSource<TaskException>();
            await Utils.C0.Sync(delegate
            {
                if (token.SessionToken.Cancelled)
                {
#if DEBUG_LOG_LOAD
                    Log("Task cancelled");
#endif
                    taskResult.SetResult(TaskException.Cancellation);
                    return;
                }
                if (!use_origin_size)
                {
                    double image_ratio = (double)image.PixelWidth / image.PixelHeight;
                    double image_height;
                    double image_width;
                    if ((image_ratio > frame_ratio) == (stretch_mode == StretchModeEnum.Uniform))
                    {
                        image_width = width * raw_pixels_per_view_pixel;
                        image_height = image_width / image_ratio;
                    }
                    else
                    {
                        image_height = height * raw_pixels_per_view_pixel;
                        image_width = image_height * image_ratio;
                    }
                    image.DecodePixelHeight = (int)image_height;
                    image.DecodePixelWidth = (int)image_width;
                }
                token.Callback.OnImageLoaded(image);
#if DEBUG_LOG_LOAD
                Log("Token " + token_processed.ToString() + "(idx=" + token.Index.ToString() + ") loaded");
#endif
                taskResult.SetResult(TaskException.Success);
            });
#if DEBUG_LOG_QUEUE
            if (s_loadQueue.PendingTaskCount <= 0)
            {
                Log("Dequeued: " + s_loadQueue.PendingTaskCount.ToString());
            }
#endif
            return taskResult.Task.Result;
        }

        private static void Log(string text)
        {
            Utils.Debug.Log("Image Loader: " + text + ".");
        }

        public interface ILoadImageCallback
        {
            void OnImageLoaded(BitmapImage image);
        }
    }
}
