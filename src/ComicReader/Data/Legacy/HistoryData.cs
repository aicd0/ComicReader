// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

#nullable disable

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Xml.Serialization;

namespace ComicReader.Data.Legacy;

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
