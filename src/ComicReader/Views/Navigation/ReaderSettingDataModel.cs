// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using ComicReader.Data;

namespace ComicReader.Views.Navigation;

internal class ReaderSettingDataModel
{
    public bool IsVertical { get; set; } = true;
    public bool IsLeftToRight { get; set; } = false;
    public bool IsVerticalContinuous { get; set; } = false;
    public bool IsHorizontalContinuous { get; set; } = false;
    public PageArrangementType VerticalPageArrangement { get; set; } = PageArrangementType.Single;
    public PageArrangementType HorizontalPageArrangement { get; set; } = PageArrangementType.DualCover;

    public bool IsContinuous
    {
        get
        {
            return IsVertical ? IsVerticalContinuous : IsHorizontalContinuous;
        }
        set
        {
            if (IsVertical)
            {
                IsVerticalContinuous = value;
            }
            else
            {
                IsHorizontalContinuous = value;
            }
        }
    }

    public PageArrangementType PageArrangement
    {
        get
        {
            return IsVertical ? VerticalPageArrangement : HorizontalPageArrangement;
        }
    }

    public ReaderSettingDataModel Clone()
    {
        var clone = new ReaderSettingDataModel();
        clone.IsVertical = IsVertical;
        clone.IsLeftToRight = IsLeftToRight;
        clone.IsVerticalContinuous = IsVerticalContinuous;
        clone.IsHorizontalContinuous = IsHorizontalContinuous;
        clone.VerticalPageArrangement = VerticalPageArrangement;
        clone.HorizontalPageArrangement = HorizontalPageArrangement;
        return clone;
    }
}
