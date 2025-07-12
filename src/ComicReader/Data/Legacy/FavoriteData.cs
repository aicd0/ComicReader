// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

#nullable disable

using System.Collections.Generic;
using System.Xml.Serialization;

namespace ComicReader.Data.Legacy;

public class FavoriteData : XmlData
{
    public List<FavoriteNodeData> RootNodes = new();

    // serialization
    public override string FileName => "Favorites";

    [XmlIgnore]
    public override XmlData Target
    {
        get => XmlDatabase.Favorites;
        set => XmlDatabase.Favorites = value as FavoriteData;
    }

    public override void Pack() { }

    public override void Unpack() { }
}

public class FavoriteNodeData
{
    [XmlAttribute]
    public string Type;
    [XmlAttribute]
    public string Name;
    [XmlAttribute]
    public long Id;
    public List<FavoriteNodeData> Children = new();
};
