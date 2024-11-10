namespace ComicReader.Common.Structs;

internal enum ZoomType
{
    CenterInside,
    CenterCrop,
}

internal class SetScrollViewerContext
{
    // Zoom
    public float? zoom = null;
    public ZoomType zoomType = ZoomType.CenterInside;
    public double? pageToApplyZoom = null;

    // Offset
    public double? horizontalOffset = null;
    public double? verticalOffset = null;

    // Animation
    public bool disableAnimation = false;
}
