// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.Xml.Serialization;

using ComicReader.Data.Models;

namespace ComicReader.Data.Legacy;

public class SettingData : XmlData
{
    public int DatabaseVersion = -1;
    public List<string> ComicFolders = new();
    public bool VerticalReading = true;
    public bool LeftToRight = false;
    public bool VerticalContinuous = true;
    public bool HorizontalContinuous = false;
    public PageArrangementEnum VerticalPageArrangement = PageArrangementEnum.Single;
    public PageArrangementEnum HorizontalPageArrangement = PageArrangementEnum.DualCoverMirror;

    // serialization
    public override string FileName => "Settings";

    [XmlIgnore]
    public override XmlData Target
    {
        get => XmlDatabase.Settings;
        set => XmlDatabase.Settings = value as SettingData;
    }
}
