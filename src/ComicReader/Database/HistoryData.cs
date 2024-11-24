// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using System.Xml.Serialization;

using ComicReader.Common;
using ComicReader.Common.Lifecycle;

namespace ComicReader.Database;

public class HistoryData : XmlData
{
    public List<HistoryItemData> Items = new();

    // serialization
    public override string FileName => "History";

    [XmlIgnore]
    public override XmlData Target
    {
        get => XmlDatabase.History;
        set => XmlDatabase.History = value as HistoryData;
    }

    public override void Pack()
    {
        foreach (HistoryItemData i in Items)
        {
            i.Pack();
        }
    }

    public override void Unpack()
    {
        foreach (HistoryItemData i in Items)
        {
            i.Unpack();
        }
    }
}

public class HistoryItemData
{
    [XmlAttribute]
    public long Id;
    [XmlAttribute]
    public string Title;
    [XmlIgnore]
    public DateTimeOffset DateTime = DateTimeOffset.MinValue;
    [XmlAttribute]
    public string DateTimePack;

    public void Pack()
    {
        DateTimePack = DateTime.ToString(CultureInfo.InvariantCulture);
    }

    public void Unpack()
    {
        DateTime = DateTimeOffset.Parse(DateTimePack, CultureInfo.InvariantCulture);
    }
}

internal class HistoryDataManager
{
    private const string TAG = "HistoryDataManager";

    public static async Task Add(long id, string title, bool sendEvent)
    {
        await XmlDatabaseManager.WaitLock();
        try
        {
            if (!XmlDatabase.Settings.SaveHistory)
            {
                return;
            }

            var record = new HistoryItemData
            {
                Id = id,
                DateTime = DateTimeOffset.Now,
                Title = title
            };

            RemoveNoLock(id);
            XmlDatabase.History.Items.Insert(0, record);
        }
        finally
        {
            XmlDatabaseManager.ReleaseLock();
        }

        OnUpdated(sendEvent);
    }

    public static async Task Remove(long id, bool sendEvent)
    {
        await XmlDatabaseManager.WaitLock();
        RemoveNoLock(id);
        XmlDatabaseManager.ReleaseLock();
        OnUpdated(sendEvent);
    }

    public static async Task Clear(bool sendEvent)
    {
        await XmlDatabaseManager.WaitLock();
        XmlDatabase.History.Items.Clear();
        XmlDatabaseManager.ReleaseLock();
        OnUpdated(sendEvent);
    }

    private static void OnUpdated(bool sendEvent)
    {
        TaskQueue.DefaultQueue.Enqueue($"{TAG}#OnUpdated", XmlDatabaseManager.SaveSealed(XmlDatabaseItem.History));

        if (sendEvent)
        {
            EventBus.Default.With(EventId.SidePaneUpdate).Emit(0);
        }
    }

    private static void RemoveNoLock(long id)
    {
        List<HistoryItemData> items = XmlDatabase.History.Items;

        for (int i = 0; i < items.Count; ++i)
        {
            HistoryItemData record = items[i];

            if (record.Id == id)
            {
                items.RemoveAt(i);
                --i;
            }
        }
    }
}
