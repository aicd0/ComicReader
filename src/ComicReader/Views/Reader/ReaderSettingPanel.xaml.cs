// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using ComicReader.Data.Models;
using ComicReader.SDK.Common.DebugTools;
using ComicReader.Views.Navigation;

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace ComicReader.Views.Reader;

internal sealed partial class ReaderSettingPanel : UserControl
{
    public delegate void DataChangedEventHandler(ReaderSettingDataModel data);
    public event DataChangedEventHandler? DataChanged;

    private ReaderSettingDataModel _model = new();

    public ReaderSettingPanel()
    {
        InitializeComponent();
    }

    public void SetData(ReaderSettingDataModel settings)
    {
        _model = settings;
        OnDataChanged();
    }

    private int PageArrangementToIndex(PageArrangementEnum pageArrangement)
    {
        switch (pageArrangement)
        {
            case PageArrangementEnum.Single:
                return 0;
            case PageArrangementEnum.DualCover:
                return 1;
            case PageArrangementEnum.DualCoverMirror:
                return 2;
            case PageArrangementEnum.DualNoCover:
                return 3;
            case PageArrangementEnum.DualNoCoverMirror:
                return 4;
            default:
                Logger.AssertNotReachHere("979D38CE673E1BC0");
                return 0;
        }
    }

    private PageArrangementEnum IndexToPageArrangement(int index)
    {
        switch (index)
        {
            case 0:
                return PageArrangementEnum.Single;
            case 1:
                return PageArrangementEnum.DualCover;
            case 2:
                return PageArrangementEnum.DualCoverMirror;
            case 3:
                return PageArrangementEnum.DualNoCover;
            case 4:
                return PageArrangementEnum.DualNoCoverMirror;
            default:
                Logger.AssertNotReachHere("B8CA81937666C2FB");
                return PageArrangementEnum.Single;
        }
    }

    private void LvPageArrangement_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        PageArrangementEnum pageArrangement = IndexToPageArrangement(LvPageArrangement.SelectedIndex);
        if (_model.IsVertical)
        {
            _model.VerticalPageArrangement = pageArrangement;
        }
        else
        {
            _model.HorizontalPageArrangement = pageArrangement;
        }

        OnDataChanged();
    }

    private void AbbVertical_Click(object sender, RoutedEventArgs e)
    {
        _model.IsVertical = false;
        OnDataChanged();
    }

    private void AbbHorizontal_Click(object sender, RoutedEventArgs e)
    {
        _model.IsVertical = true;
        OnDataChanged();
    }

    private void AbbLeftToRight_Click(object sender, RoutedEventArgs e)
    {
        _model.IsLeftToRight = false;
        OnDataChanged();
    }

    private void AbbRightToLeft_Click(object sender, RoutedEventArgs e)
    {
        _model.IsLeftToRight = true;
        OnDataChanged();
    }

    private void AbbSeperate_Click(object sender, RoutedEventArgs e)
    {
        _model.IsContinuous = true;
        OnDataChanged();
    }

    private void AbbContinuous_Click(object sender, RoutedEventArgs e)
    {
        _model.IsContinuous = false;
        OnDataChanged();
    }

    private void OnDataChanged()
    {
        PageArrangementEnum pageArrangement = _model.IsVertical ? _model.VerticalPageArrangement : _model.HorizontalPageArrangement;
        LvPageArrangement.SelectedIndex = PageArrangementToIndex(pageArrangement);
        PdsDemoSingle1.IsHighlight = pageArrangement == PageArrangementEnum.Single;
        PdsDemoSingle2.IsHighlight = pageArrangement == PageArrangementEnum.Single;
        PdsDemoSingle3.IsHighlight = pageArrangement == PageArrangementEnum.Single;
        PdsDemoSingle4.IsHighlight = pageArrangement == PageArrangementEnum.Single;
        PdsDemoSingle5.IsHighlight = pageArrangement == PageArrangementEnum.Single;
        PdsDemoDual1.IsHighlight = pageArrangement == PageArrangementEnum.DualCover;
        PdsDemoDual2.IsHighlight = pageArrangement == PageArrangementEnum.DualCover;
        PdsDemoDual3.IsHighlight = pageArrangement == PageArrangementEnum.DualCover;
        PdsDemoDualCoverMirror1.IsHighlight = pageArrangement == PageArrangementEnum.DualCoverMirror;
        PdsDemoDualCoverMirror2.IsHighlight = pageArrangement == PageArrangementEnum.DualCoverMirror;
        PdsDemoDualCoverMirror3.IsHighlight = pageArrangement == PageArrangementEnum.DualCoverMirror;
        PdsDemoDualNoCover1.IsHighlight = pageArrangement == PageArrangementEnum.DualNoCover;
        PdsDemoDualNoCover2.IsHighlight = pageArrangement == PageArrangementEnum.DualNoCover;
        PdsDemoDualNoCover3.IsHighlight = pageArrangement == PageArrangementEnum.DualNoCover;
        PdsDemoDualNoCoverMirror1.IsHighlight = pageArrangement == PageArrangementEnum.DualNoCoverMirror;
        PdsDemoDualNoCoverMirror2.IsHighlight = pageArrangement == PageArrangementEnum.DualNoCoverMirror;
        PdsDemoDualNoCoverMirror3.IsHighlight = pageArrangement == PageArrangementEnum.DualNoCoverMirror;

        FlowDirection flowDirection = _model.IsLeftToRight ? FlowDirection.LeftToRight : FlowDirection.RightToLeft;
        FlowDirection demoPageFlowDirection = _model.IsVertical ? FlowDirection.LeftToRight : flowDirection;
        SpDemoSingle.FlowDirection = demoPageFlowDirection;
        SpDemoDualCover.FlowDirection = demoPageFlowDirection;
        SpDemoDualCoverMirror.FlowDirection = demoPageFlowDirection;
        SpDemoDualNoCover.FlowDirection = demoPageFlowDirection;
        SpDemoDualNoCoverMirror.FlowDirection = demoPageFlowDirection;

        AbbVertical.Visibility = _model.IsVertical ? Visibility.Visible : Visibility.Collapsed;
        AbbHorizontal.Visibility = _model.IsVertical ? Visibility.Collapsed : Visibility.Visible;
        AbbLeftToRight.Visibility = !_model.IsVertical && _model.IsLeftToRight ? Visibility.Visible : Visibility.Collapsed;
        AbbRightToLeft.Visibility = !_model.IsVertical && !_model.IsLeftToRight ? Visibility.Visible : Visibility.Collapsed;
        AbbContinuous.Visibility = _model.IsContinuous ? Visibility.Visible : Visibility.Collapsed;
        AbbSeperate.Visibility = _model.IsContinuous ? Visibility.Collapsed : Visibility.Visible;

        DataChanged?.Invoke(_model);
    }
}
