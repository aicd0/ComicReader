// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;

using ComicReader.Native;
using ComicReader.Utils;

using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;

using Windows.Storage.Streams;

using WinRT.Interop;

namespace ComicReader.Common.SimpleImageView;

internal partial class SimpleImageView : UserControl
{
    private static double s_rawPixelPerPixel = -1;

    private bool _isLoaded;
    private Model _model;
    private int _currentImageHash = 0;
    private readonly CancellationSession _cancellationSession = new();
    private readonly WeakImageResultHandler _handler;

    public SimpleImageView()
    {
        InitializeComponent();

        Loaded += OnLoaded;
        Unloaded += OnUnloaded;

        _handler = new(this);
    }

    public void SetModel(Model model)
    {
        _model = model;
        UpdateImage();
    }

    public void UnsetModel()
    {
        _model = null;
        UpdateImage();
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _isLoaded = true;
        UpdateImage();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        _isLoaded = false;
        UpdateImage();
    }

    private void UpdateImage()
    {
        Model model = _model;
        if (!_isLoaded || model == null)
        {
            UnloadImage();
            return;
        }

        int newHash = model.GetImageHashCode();
        if (newHash == _currentImageHash)
        {
            return;
        }

        UnloadImage();

        _currentImageHash = newHash;
        CancellationSession.SessionToken token = _cancellationSession.Token;
        Action loadAction = delegate
        {
            LoadSingleImage(token, model, _handler);
        };
        model.Dispatcher.Queue(loadAction, model.DebugDescription);
    }

    private void UnloadImage()
    {
        _currentImageHash = 0;
        _cancellationSession.Next();

        if (ImageHolder.Source != null)
        {
            ImageHolder.Source = null;
        }
    }

    private static void LoadSingleImage(CancellationSession.SessionToken token, Model model, WeakImageResultHandler handler)
    {
        if (token.IsCancellationRequested)
        {
            // cancelled
            return;
        }

        double width = model.Width * model.Multiplication;
        double height = model.Height * model.Multiplication;

        bool use_origin_size = double.IsInfinity(width) && double.IsInfinity(height);
        double raw_pixels_per_view_pixel = GetRawPixelPerPixel();
        double frame_ratio = width / height;

        BitmapImage image = null;
        using (IRandomAccessStream stream = model.Source.GetImageStream())
        {
            if (stream == null)
            {
                // failure
                return;
            }

            stream.Seek(0);
            bool imgLoadSuccess = Threading.RunInMainThreadAsync(async delegate
            {
                if (token.IsCancellationRequested)
                {
                    // cancelled
                    return false;
                }

                image = new BitmapImage();
                try
                {
                    await image.SetSourceAsync(stream).AsTask();
                }
                catch (Exception)
                {
                    // failure
                    return false;
                }
                return true;
            }).Result;
            if (!imgLoadSuccess)
            {
                // failure
                return;
            }
        }

        _ = Threading.RunInMainThread(delegate
        {
            if (token.IsCancellationRequested)
            {
                // cancelled
                return;
            }

            if (!use_origin_size)
            {
                double image_ratio = (double)image.PixelWidth / image.PixelHeight;
                double image_height;
                double image_width;
                if ((image_ratio > frame_ratio) == (model.StretchMode == StretchModeEnum.Uniform))
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

            handler.OnSuccess(image);
        });
    }

    private static double GetRawPixelPerPixel()
    {
        if (s_rawPixelPerPixel < 0)
        {
            s_rawPixelPerPixel = GetScaleAdjustment();
        }

        return s_rawPixelPerPixel;
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

    private class WeakImageResultHandler
    {
        private readonly WeakReference<SimpleImageView> _imageView;

        public WeakImageResultHandler(SimpleImageView view)
        {
            _imageView = new WeakReference<SimpleImageView>(view);
        }

        public void OnSuccess(BitmapImage image)
        {
            if (_imageView.TryGetTarget(out SimpleImageView view))
            {
                view.ImageHolder.Source = image;
            }
        }
    }

    public interface IImageSource
    {
        IRandomAccessStream GetImageStream();
    }

    public interface IDispatcher
    {
        void Queue(Action action, string debugDescription);
    }

    public enum StretchModeEnum
    {
        Uniform,
        UniformToFill,
    }

    public class Model
    {
        public IImageSource Source { get; set; }
        public double Width { get; set; } = double.PositiveInfinity;
        public double Height { get; set; } = double.PositiveInfinity;
        public StretchModeEnum StretchMode { get; set; } = StretchModeEnum.Uniform;
        public double Multiplication { get; set; } = 1.0;
        public IDispatcher Dispatcher { get; set; }
        public string DebugDescription { get; set; }

        public int GetImageHashCode()
        {
            return HashCode.Combine(
                Source.GetHashCode(),
                Width,
                Height,
                StretchMode,
                Multiplication);
        }
    }
}
