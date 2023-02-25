namespace ComicReader.Utils
{
    internal static class Extensions
    {
        public static bool Successful(this TaskException r)
        {
            return r == TaskException.Success;
        }
    }
}
