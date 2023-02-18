using Windows.UI.Core;
using Windows.UI.Xaml;

namespace ComicReader.Utils
{
    internal static class ScreenUtils
    {
        public static bool IsPointerInApp()
        {
            var pointerPosition = CoreWindow.GetForCurrentThread().PointerPosition;
            return Window.Current.Bounds.Contains(pointerPosition);
        }
    }
}
