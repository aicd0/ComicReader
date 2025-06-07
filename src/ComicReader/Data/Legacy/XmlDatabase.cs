// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

#nullable disable

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Serialization;

using ComicReader.Common;
using ComicReader.SDK.Common.DebugTools;

using Windows.Storage;
using Windows.Storage.Streams;

namespace ComicReader.Data.Legacy;

public abstract class XmlData
{
    public abstract string FileName { get; }
    [XmlIgnore]
    public abstract XmlData Target { get; set; }

    public virtual void Pack() { }
    public virtual void Unpack() { }
}

internal class XmlDatabase
{
    public static SettingData Settings = new();
    public static FavoriteData Favorites = new();
    public static HistoryData History = new();
};

internal enum XmlDatabaseItem
{
    Favorites,
    History,
    Settings
}

internal class XmlDatabaseManager
{
    private const string TAG = "XmlDatabaseManager";

    private static StorageFolder DatabaseFolder => ApplicationData.Current.LocalFolder;

    private static bool m_database_ready = false;
    private static readonly SemaphoreSlim m_database_lock = new(1);

    public static async Task WaitLock()
    {
        await C0.WaitFor(() => m_database_ready);
        await m_database_lock.WaitAsync();
    }

    public static void ReleaseLock()
    {
        m_database_lock.Release();
    }

    public static async Task<TaskException> Initialize()
    {
        await Load(XmlDatabase.Settings);
        await Load(XmlDatabase.Favorites);
        await Load(XmlDatabase.History);

        m_database_ready = true;
        return TaskException.Success;
    }

    private static async Task<TaskException> Load(XmlData obj)
    {
        object file = await Storage.TryGetFile(DatabaseFolder, obj.FileName);
        if (file == null)
        {
            return TaskException.FileNotFound;
        }

        IRandomAccessStream stream = await (file as StorageFile).OpenAsync(FileAccessMode.Read);
        var serializer = new XmlSerializer(obj.GetType());
        serializer.UnknownAttribute += (x, y) => Log("UnknownAttribute: " + y.ToString());
        serializer.UnknownElement += (x, y) => Log("UnknownElement: " + y.ToString());
        serializer.UnknownNode += (x, y) => Log("UnknownNode: " + y.ToString());
        serializer.UnreferencedObject += (x, y) => Log("UnreferencedObject: " + y.ToString());

        try
        {
            obj.Target = serializer.Deserialize(stream.AsStream()) as XmlData;
        }
        catch (Exception)
        {
            return TaskException.Failure;
        }

        obj.Target.Unpack();
        stream.Dispose();
        return TaskException.Success;
    }

    public static Action SaveSealed(XmlDatabaseItem item) =>
        () => SaveUnsealed(item).Wait();

    public static async Task<TaskException> SaveUnsealed(XmlDatabaseItem item)
    {
        Logger.I(TAG, "Saving: " + item.ToString());
        switch (item)
        {
            case XmlDatabaseItem.Favorites:
                await Save(XmlDatabase.Favorites);
                break;
            case XmlDatabaseItem.History:
                await Save(XmlDatabase.History);
                break;
            case XmlDatabaseItem.Settings:
                await Save(XmlDatabase.Settings);
                break;
            default:
                Logger.F(TAG, "SaveXmlDatabaseUnknownItem");
                return TaskException.InvalidParameters;
        }

        return TaskException.Success;
    }

    private static async Task Save(XmlData obj)
    {
        await WaitLock();
        StorageFile file = await DatabaseFolder.CreateFileAsync(
            obj.FileName, CreationCollisionOption.ReplaceExisting);
        IRandomAccessStream stream = await file.OpenAsync(
            FileAccessMode.ReadWrite);

        obj.Pack();
        var serializer = new XmlSerializer(obj.GetType());
        serializer.Serialize(stream.AsStream(), obj);

        stream.Dispose();
        ReleaseLock();
    }

    private static void Log(string message)
    {
        Logger.I(TAG, message);
    }
}
