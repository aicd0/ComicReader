// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.Xml.Serialization;

namespace ComicReader.Data.Legacy;

public class SettingData : XmlData
{
    public int DatabaseVersion = -1;
    public List<string> ComicFolders = new();
    public bool VerticalReading = true;
    public bool LeftToRight = false;
    public bool VerticalContinuous = true;
    public bool HorizontalContinuous = false;
    public PageArrangementType VerticalPageArrangement = PageArrangementType.Single;
    public PageArrangementType HorizontalPageArrangement = PageArrangementType.DualCoverMirror;

    // serialization
    public override string FileName => "Settings";

    [XmlIgnore]
    public override XmlData Target
    {
        get => XmlDatabase.Settings;
        set => XmlDatabase.Settings = value as SettingData;
    }
}

public enum PageArrangementType
{
    Single, // 1 2 3 4 5
    DualCover, // 1 23 45
    DualCoverMirror, // 1 32 54
    DualNoCover, // 12 34 5
    DualNoCoverMirror, // 21 43 5
}
