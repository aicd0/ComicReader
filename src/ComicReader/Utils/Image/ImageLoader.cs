#if DEBUG
//#define DEBUG_LOG_LOAD
//#define DEBUG_LOG_QUEUE
#endif

using ComicReader.Database;
using ComicReader.Native;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml.Media.Imaging;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Windows.Storage.Streams;
using WinRT.Interop;

namespace ComicReader.Utils.Image;

internal static class ImageLoader
{
    private static readonly TaskQueue s_loadQueue = new TaskQueue("ImageLoader");
    private static double s_rawPixelPerPixel = -1;

    public sealed class Transaction : BaseTransaction<TaskException>
    {
        private readonly List<Token> _tokens;
        private TaskQueue _queue;
        private double _width = double.PositiveInfinity;
        private double _height = double.PositiveInfinity;
        private StretchModeEnum _stretchMode = StretchModeEnum.Uniform;
        private double _multiplication = 1.0;

        public Transaction(List<Token> tokens)
        {
            _tokens = tokens;
        }

        public Transaction SetWidthConstraint(double value)
        {
            _width = value;
            return this;
        }

        public Transaction SetHeightConstraint(double value)
        {
            _height = value;
            return this;
        }

        public Transaction SetStretchMode(StretchModeEnum mode)
        {
            _stretchMode = mode;
            return this;
        }

        public Transaction SetDecodePixelMultiplication(double value)
        {
            _multiplication = value;
            return this;
        }

        public Transaction SetQueue(TaskQueue queue)
        {
            _queue = queue;
            return this;
        }

        protected override TaskException CommitImpl()
        {
            _width *= _multiplication;
            _height *= _multiplication;

            _queue ??= s_loadQueue;

            foreach (Token token in _tokens)
            {
                if (token.Callback == null || token.Comic == null || token.SessionToken == null)
                {
                    continue;
                }

                TaskException task() => LoadSingleImage(token, _width, _height, _stretchMode).Result;
                _queue.Enqueue("ImageLoader#CommitImpl", task);
            }
#if DEBUG_LOG_QUEUE
            Log("Enqueued: " + _queue.PendingTaskCount.ToString());
#endif
            return TaskException.Success;
        }
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
        public ICallback Callback;
    }

    public interface ICallback
    {
        void OnSuccess(BitmapImage image);
    }

    private static double GetRawPixelPerPixel()
    {
        if (s_rawPixelPerPixel < 0)
        {
            s_rawPixelPerPixel = GetScaleAdjustment();
        }

        return s_rawPixelPerPixel;
    }

    private static async Task<TaskException> LoadSingleImage(
        Token token,
        double width, double height,
        StretchModeEnum stretch_mode
    )
    {
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
        double raw_pixels_per_view_pixel = GetRawPixelPerPixel();
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
            bool imgLoadSuccess = await Threading.RunInMainThreadAsync(async delegate
            {
                image = new BitmapImage();
                try
                {
                    await image.SetSourceAsync(stream).AsTask();
                }
                catch (Exception e)
                {
                    Log("Skipped token " + token.Index.ToString() + ", image corrupted. " + e.ToString());
                    return false;
                }
                return true;
            });
            if (!imgLoadSuccess)
            {
                return TaskException.Failure;
            }
        }

        return await Threading.RunInMainThread(delegate
        {
            if (token.SessionToken.Cancelled)
            {
#if DEBUG_LOG_LOAD
                Log("Task cancelled");
#endif
                return TaskException.Cancellation;
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

            token.Callback.OnSuccess(image);
#if DEBUG_LOG_LOAD
            Log("Token " + token_processed.ToString() + "(idx=" + token.Index.ToString() + ") loaded");
#endif
            return TaskException.Success;
        });
    }

    private static void Log(string message)
    {
        Logger.I("ImageLoader", message);
    }

    private static double GetScaleAdjustment()
    {
        IntPtr hWnd = WindowNative.GetWindowHandle(App.Window);
        WindowId wndId = Win32Interop.GetWindowIdFromWindow(hWnd);
        var displayArea = DisplayArea.GetFromWindowId(wndId, DisplayAreaFallback.Primary);
        IntPtr hMonitor = Win32Interop.GetMonitorFromDisplayId(displayArea.DisplayId);

        // Get DPI.
        int result = NativeMethods.GetDpiForMonitor(hMonitor, NativeModels.MonitorDPIType.MDT_Default, out uint dpiX, out uint _);
        if (result != 0)
        {
            throw new Exception("Could not get DPI for monitor.");
        }

        uint scaleFactorPercent = (uint)(((long)dpiX * 100 + (96 >> 1)) / 96);
        return scaleFactorPercent / 100.0;
    }
}
