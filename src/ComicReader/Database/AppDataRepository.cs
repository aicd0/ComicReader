using ComicReader.Views.Navigation;

namespace ComicReader.Database;
internal static class AppDataRepository
{
    public static ReaderSettingDataModel ReaderSettings { get; } = new();

    public static void Initialize()
    {
        ReaderSettings.IsVertical = XmlDatabase.Settings.VerticalReading;
        ReaderSettings.IsLeftToRight = XmlDatabase.Settings.LeftToRight;
        ReaderSettings.IsVerticalContinuous = XmlDatabase.Settings.VerticalContinuous;
        ReaderSettings.IsHorizontalContinuous = XmlDatabase.Settings.HorizontalContinuous;
        ReaderSettings.VerticalPageArrangement = XmlDatabase.Settings.VerticalPageArrangement;
        ReaderSettings.HorizontalPageArrangement = XmlDatabase.Settings.HorizontalPageArrangement;
    }
}
