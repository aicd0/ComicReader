#if DEBUG
//#define DEBUG_LOG_LOAD
#endif

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Windows.Graphics.Display;
using Windows.Storage.Streams;
using Windows.UI.Xaml.Media.Imaging;
using ComicReader.Database;

namespace ComicReader.Utils
{
    using RawTask = Task<Utils.TaskResult>;

    public class ImageLoader
    {
        private ImageLoader() {}

        public enum StretchModeEnum
        {
            Uniform,
            UniformToFill,
        }

        public enum ConstrainType
        {
            Exact,
            MatchFirstImage,
        }

        public class DimensionConstrain
        {
            public ConstrainType Type;
            public double Val;

            public static implicit operator DimensionConstrain(double val)
            {
                System.Diagnostics.Debug.Assert(!double.IsNaN(val));

                return new DimensionConstrain
                {
                    Type = ConstrainType.Exact,
                    Val = val,
                };
            }

            public static implicit operator DimensionConstrain(ConstrainType type)
            {
                return new DimensionConstrain
                {
                    Type = type,
                    Val = 0.0,
                };
            }
        }

        public class Token
        {
            public ComicData Comic;
            public int Index;
            public Action<BitmapImage> Callback;
            public Func<BitmapImage, Task> CallbackAsync;
        }

        public sealed class Builder : BuilderBase<RawTask>
        {
            private LockContext m_db;
            private List<Token> m_tokens;
            private DimensionConstrain m_width_constrain = double.PositiveInfinity;
            private DimensionConstrain m_height_constrain = double.PositiveInfinity;
            private StretchModeEnum m_stretch_mode = StretchModeEnum.Uniform;
            private CancellationLock m_cancellation_lock;
            private double m_multiplication = 1.0;

            public Builder(LockContext db, List<Token> tokens, CancellationLock cancellation_lock)
            {
                m_db = db;
                m_tokens = tokens;
                m_cancellation_lock = cancellation_lock;
            }

            public Builder WidthConstrain(DimensionConstrain constrain)
            {
                m_width_constrain = constrain;
                return this;
            }

            public Builder HeightConstrain(DimensionConstrain constrain)
            {
                m_height_constrain = constrain;
                return this;
            }

            public Builder StretchMode(StretchModeEnum mode)
            {
                m_stretch_mode = mode;
                return this;
            }

            public Builder Multiplication(double value)
            {
                m_multiplication = value;
                return this;
            }

            protected override RawTask CommitImpl()
            {
                m_width_constrain.Val *= m_multiplication;
                m_height_constrain.Val *= m_multiplication;
                return Load(m_db, m_tokens, m_width_constrain, m_height_constrain, m_stretch_mode, m_cancellation_lock);
            }
        }

        private static async RawTask Load(LockContext db, IEnumerable<Token> tokens,
            DimensionConstrain width_constrain, DimensionConstrain height_constrain,
            StretchModeEnum stretch_mode, CancellationLock cancellation_lock)
        {
            List<Token> tokens_cpy = new List<Token>(tokens);

#if DEBUG_LOG_LOAD
            Log("Loading " + tokens_cpy.Count.ToString() + " images");
#endif

            bool use_origin_size =
                width_constrain.Type == ConstrainType.Exact &&
                double.IsInfinity(width_constrain.Val) &&
                height_constrain.Type == ConstrainType.Exact &&
                double.IsInfinity(height_constrain.Val);

            double raw_pixels_per_view_pixel = 0.0;
            double frame_ratio = 0.0;
            bool first_token = true;
            bool all_token_success = true;

            for (int token_processed = 0; tokens_cpy.Count > 0; ++token_processed)
            {
                if (cancellation_lock.CancellationRequested)
                {
#if DEBUG_LOG_LOAD
                    Log("Task cancelled");
#endif
                    return new TaskResult(TaskException.Cancellation);
                }

                Token token = tokens_cpy.First();
                tokens_cpy.RemoveAt(0);

                // Lazy load.
                ComicData comic = token.Comic;
                TaskResult result = await comic.UpdateImages(db, cover_only: token.Index < 0, reload: false);

                // Skip tokens whose comic folder cannot be reached.
                if (!result.Successful)
                {
                    Log("Token " + token.Index.ToString() + "(" + token_processed.ToString() + ") skipped, failed to update comic images");
                    int token_before = tokens_cpy.Count;
                    tokens_cpy.RemoveAll(x => x.Comic == comic);
                    Log((token_before - tokens_cpy.Count).ToString() + " tokens with the same comic were removed.");
                    all_token_success = false;
                    continue;
                }

                BitmapImage image = null;

                using (IRandomAccessStream stream = await comic.GetImageStream(db, token.Index))
                {
                    if (stream == null)
                    {
                        Log("Failed to get img stream " + token.Index.ToString() + ", skipped");
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
                            Log("Skipped token " + token.Index.ToString() + ", image corrupted. " + e.ToString());
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

                            if (width_constrain.Type == ConstrainType.MatchFirstImage)
                            {
                                width_constrain.Val = image.PixelWidth;
                            }

                            if (height_constrain.Type == ConstrainType.MatchFirstImage)
                            {
                                height_constrain.Val = image.PixelHeight;
                            }

                            raw_pixels_per_view_pixel = DisplayInformation.GetForCurrentView().RawPixelsPerViewPixel;
                            frame_ratio = width_constrain.Val / height_constrain.Val;
                        }

                        if (!use_origin_size)
                        {
                            double image_ratio = (double)image.PixelWidth / image.PixelHeight;

                            double image_height;
                            double image_width;
                            if ((image_ratio > frame_ratio) == (stretch_mode == StretchModeEnum.Uniform))
                            {
                                image_width = width_constrain.Val * raw_pixels_per_view_pixel;
                                image_height = image_width / image_ratio;
                            }
                            else
                            {
                                image_height = height_constrain.Val * raw_pixels_per_view_pixel;
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
                        Log("Token " + token_i.ToString() + " (idx=" + token.Index.ToString() + ") loaded");
#endif

                        completion_src.SetResult(true);
                    });

                    await completion_src.Task;
                }
            }

#if DEBUG_LOG_LOAD
            Log("All tokens loaded (trig_update=" + trig_update.ToString() + ")");
#endif

            if (!all_token_success)
            {
                return new TaskResult(TaskException.Failure);
            }

            return new TaskResult();
        }

        private static void Log(string text)
        {
            Utils.Debug.Log("Image Loader: " + text);
        }
    }
}
