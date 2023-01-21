namespace ComicReader.Common.Structs
{
    internal enum ZoomType
    {
        CenterInside,
        CenterCrop,
    }

    internal class SetScrollViewerContext
    {
        // Zoom
        public float? Zoom = null;
        public ZoomType zoomType = ZoomType.CenterInside;
        public double? PageToApplyZoom = null;

        // Offset
        public double? HorizontalOffset = null;
        public double? VerticalOffset = null;

        // Animation
        public bool DisableAnimation = false;
    }
}
