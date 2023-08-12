using System;
using Windows.Foundation;
using Windows.UI.Core;
using Windows.UI.Xaml;

namespace ComicReader.Utils
{
    internal static class ScreenUtils
    {
        public static bool? IsPointerInApp()
        {
            var pointerPosition = GetPointPosition();
            if (!pointerPosition.HasValue)
            {
                return null;
            }
            return Window.Current.Bounds.Contains(pointerPosition.Value);
        }

        public static Point? GetPointPosition()
        {
            try
            {
                return CoreWindow.GetForCurrentThread().PointerPosition;
            }
            catch (UnauthorizedAccessException)
            {
                return null;
            }
        }
    }
}
