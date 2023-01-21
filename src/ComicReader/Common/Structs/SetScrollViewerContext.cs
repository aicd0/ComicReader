namespace ComicReader.Common.Structs
{
    internal class SetScrollViewerContext
    {
        // Zoom
        public float? Zoom = null;
        public double? PageToApplyZoom = null;

        // Offset
        public double? HorizontalOffset = null;
        public double? VerticalOffset = null;

        // Animation
        public bool DisableAnimation = false;
    }
}
