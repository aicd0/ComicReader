// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;

using ComicReader.Utils;

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;

namespace ComicReader.Common.SimpleImageView;

internal partial class SimpleImageView : UserControl
{
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

        BitmapImage image = ImageCacheDatabase.GetImage(token, model.Source, model.FrameWidth, model.FrameHeight, model.StretchMode);
        if (image == null)
        {
            // failure
            return;
        }

        _ = Threading.RunInMainThread(delegate
        {
            if (token.IsCancellationRequested)
            {
                // cancelled
                return;
            }

            handler.OnSuccess(image);
        });
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

    public interface IDispatcher
    {
        void Queue(Action action, string debugDescription);
    }

    public class Model
    {
        public IImageSource Source { get; set; }
        public double Width { private get; set; } = double.PositiveInfinity;
        public double Height { private get; set; } = double.PositiveInfinity;
        public StretchModeEnum StretchMode { get; set; } = StretchModeEnum.Uniform;
        public double Multiplication { get; set; } = 1.0;
        public IDispatcher Dispatcher { get; set; }
        public string DebugDescription { get; set; }
        public double FrameWidth => Width * Multiplication;
        public double FrameHeight => Height * Multiplication;

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
