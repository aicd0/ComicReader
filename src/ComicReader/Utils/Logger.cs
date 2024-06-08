namespace ComicReader.Utils;

internal static class Logger
{
    public static void I(string tag, string message)
    {
        string realMessage = $"[{tag}] {message}";
        Debug.Log(realMessage);
    }
}
