// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using ComicReader.Common.DebugTools;
using ComicReader.Data.Legacy;
using ComicReader.Views.Navigation;

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace ComicReader.Views.Reader;

internal sealed partial class ReaderSettingPanel : UserControl
{
    public delegate void DataChangedEventHandler(ReaderSettingDataModel data);
    public event DataChangedEventHandler DataChanged;

    private ReaderSettingDataModel _model;

    public ReaderSettingPanel()
    {
        InitializeComponent();
    }

    public void SetData(ReaderSettingDataModel settings)
    {
        _model = settings;
        OnDataChanged();
    }

    private int PageArrangementToIndex(PageArrangementType pageArrangement)
    {
        switch (pageArrangement)
        {
            case PageArrangementType.Single:
                return 0;
            case PageArrangementType.DualCover:
                return 1;
            case PageArrangementType.DualCoverMirror:
                return 2;
            case PageArrangementType.DualNoCover:
                return 3;
            case PageArrangementType.DualNoCoverMirror:
                return 4;
            default:
                DebugUtils.Assert(false);
                return 0;
        }
    }

    private PageArrangementType IndexToPageArrangement(int index)
    {
        switch (index)
        {
            case 0:
                return PageArrangementType.Single;
            case 1:
                return PageArrangementType.DualCover;
            case 2:
                return PageArrangementType.DualCoverMirror;
            case 3:
                return PageArrangementType.DualNoCover;
            case 4:
                return PageArrangementType.DualNoCoverMirror;
            default:
                DebugUtils.Assert(false);
                return PageArrangementType.Single;
        }
    }

    private void LvPageArrangement_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        PageArrangementType pageArrangement = IndexToPageArrangement(LvPageArrangement.SelectedIndex);
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
        PageArrangementType pageArrangement = _model.IsVertical ? _model.VerticalPageArrangement : _model.HorizontalPageArrangement;
        LvPageArrangement.SelectedIndex = PageArrangementToIndex(pageArrangement);
        PdsDemoSingle1.IsHighlight = pageArrangement == PageArrangementType.Single;
        PdsDemoSingle2.IsHighlight = pageArrangement == PageArrangementType.Single;
        PdsDemoSingle3.IsHighlight = pageArrangement == PageArrangementType.Single;
        PdsDemoSingle4.IsHighlight = pageArrangement == PageArrangementType.Single;
        PdsDemoSingle5.IsHighlight = pageArrangement == PageArrangementType.Single;
        PdsDemoDual1.IsHighlight = pageArrangement == PageArrangementType.DualCover;
        PdsDemoDual2.IsHighlight = pageArrangement == PageArrangementType.DualCover;
        PdsDemoDual3.IsHighlight = pageArrangement == PageArrangementType.DualCover;
        PdsDemoDualCoverMirror1.IsHighlight = pageArrangement == PageArrangementType.DualCoverMirror;
        PdsDemoDualCoverMirror2.IsHighlight = pageArrangement == PageArrangementType.DualCoverMirror;
        PdsDemoDualCoverMirror3.IsHighlight = pageArrangement == PageArrangementType.DualCoverMirror;
        PdsDemoDualNoCover1.IsHighlight = pageArrangement == PageArrangementType.DualNoCover;
        PdsDemoDualNoCover2.IsHighlight = pageArrangement == PageArrangementType.DualNoCover;
        PdsDemoDualNoCover3.IsHighlight = pageArrangement == PageArrangementType.DualNoCover;
        PdsDemoDualNoCoverMirror1.IsHighlight = pageArrangement == PageArrangementType.DualNoCoverMirror;
        PdsDemoDualNoCoverMirror2.IsHighlight = pageArrangement == PageArrangementType.DualNoCoverMirror;
        PdsDemoDualNoCoverMirror3.IsHighlight = pageArrangement == PageArrangementType.DualNoCoverMirror;

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
