namespace ComicReader.Utils;

internal class CancellationSession
{
    private Token _token = new();

    public Token CurrentToken => _token;

    public Token Next()
    {
        _token.Cancel();
        _token = new Token();
        return _token;
    }

    public class Token
    {
        private bool _cancelled = false;

        public bool Cancelled => _cancelled;

        public void Cancel()
        {
            _cancelled = true;
        }
    }
}
