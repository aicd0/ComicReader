// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using ComicReader.Data.Models;

namespace ComicReader.Views.Navigation;

internal class ReaderSettingDataModel
{
    public bool IsVertical { get; set; } = true;
    public bool IsLeftToRight { get; set; } = false;
    public bool IsVerticalContinuous { get; set; } = false;
    public bool IsHorizontalContinuous { get; set; } = false;
    public PageArrangementEnum VerticalPageArrangement { get; set; } = PageArrangementEnum.Single;
    public PageArrangementEnum HorizontalPageArrangement { get; set; } = PageArrangementEnum.DualCover;

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

    public PageArrangementEnum PageArrangement
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
