using ComicReader.Views.Navigation;
using System.Collections.Concurrent;
using System.Threading;

namespace ComicReader.Database;
internal static class AppDataRepository
{
    private static int _nextComicToken = 0;

    private static readonly ReaderSettingDataModel sReaderSettings = new();
    private static readonly ConcurrentDictionary<string, ComicData> sComicMap = new();

    public static void Initialize()
    {
        sReaderSettings.IsVertical = XmlDatabase.Settings.VerticalReading;
        sReaderSettings.IsLeftToRight = XmlDatabase.Settings.LeftToRight;
        sReaderSettings.IsVerticalContinuous = XmlDatabase.Settings.VerticalContinuous;
        sReaderSettings.IsHorizontalContinuous = XmlDatabase.Settings.HorizontalContinuous;
        sReaderSettings.VerticalPageArrangement = XmlDatabase.Settings.VerticalPageArrangement;
        sReaderSettings.HorizontalPageArrangement = XmlDatabase.Settings.HorizontalPageArrangement;
    }

    public static ReaderSettingDataModel GetReaderSetting()
    {
        return sReaderSettings;
    }

    public static ComicData GetComicData(string token)
    {
        return sComicMap.TryGetValue(token, out ComicData comicData) ? comicData : null;
    }

    public static string PutComicData(ComicData comicData)
    {
        string token = (Interlocked.Increment(ref _nextComicToken) - 1).ToString();
        sComicMap[token] = comicData;
        return token;
    }
}
