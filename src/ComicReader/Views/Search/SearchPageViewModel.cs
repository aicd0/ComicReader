// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.ObjectModel;
using System.ComponentModel;

using ComicReader.ViewModels;

using Microsoft.UI.Xaml.Controls;

namespace ComicReader.Views.Search;

internal class SearchPageViewModel : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler PropertyChanged;

    public bool IsLoading;

    public bool IsResultEmpty => SearchResults.Count == 0;

    public void UpdateUI()
    {
        IsLoadingRingVisible = IsLoading;
        IsResultGridVisible = !IsLoading && !IsResultEmpty;
        IsNoResultTextVisible = !IsLoading && IsResultEmpty;
    }

    // UI elements
    public ObservableCollection<ComicItemViewModel> SearchResults = [];

    private bool m_IsLoadingRingVisible;
    public bool IsLoadingRingVisible
    {
        get => m_IsLoadingRingVisible;
        set
        {
            m_IsLoadingRingVisible = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("IsLoadingRingVisible"));
        }
    }

    private string _title = "";
    public string Title
    {
        get => _title;
        set
        {
            _title = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Title"));
        }
    }

    private string _filterDetails = "";
    public string FilterDetails
    {
        get => _filterDetails;
        set
        {
            _filterDetails = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("FilterDetails"));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("FilterDetailsVisible"));
        }
    }
    public bool FilterDetailsVisible => FilterDetails.Length > 0;

    private bool m_IsResultGridVisible;
    public bool IsResultGridVisible
    {
        get => m_IsResultGridVisible;
        set
        {
            m_IsResultGridVisible = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("IsResultGridVisible"));
        }
    }

    private string m_NoResultText;
    public string NoResultText
    {
        get => m_NoResultText;
        set
        {
            m_NoResultText = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("NoResultText"));
        }
    }

    private bool m_IsNoResultTextVisible;
    public bool IsNoResultTextVisible
    {
        get => m_IsNoResultTextVisible;
        set
        {
            m_IsNoResultTextVisible = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("IsNoResultTextVisible"));
        }
    }

    private bool m_IsSelectMode = false;
    public bool IsSelectMode
    {
        get => m_IsSelectMode;
        set
        {
            m_IsSelectMode = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs($"{nameof(IsSelectMode)}"));
        }
    }

    private ListViewSelectionMode m_ComicItemSelectionMode = ListViewSelectionMode.None;
    public ListViewSelectionMode ComicItemSelectionMode
    {
        get => m_ComicItemSelectionMode;
        set
        {
            m_ComicItemSelectionMode = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs($"{nameof(ComicItemSelectionMode)}"));
        }
    }

    public bool CommandBarSelectAllToggleOmitOnce = false;
    private bool m_IsCommandBarSelectAllToggled = false;
    public bool IsCommandBarSelectAllToggled
    {
        get => m_IsCommandBarSelectAllToggled;
        set
        {
            if (m_IsCommandBarSelectAllToggled != value)
            {
                if (!CommandBarSelectAllToggleOmitOnce)
                {
                    OnCommandBarSelectAllToggleChanged(value);
                }

                m_IsCommandBarSelectAllToggled = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("IsCommandBarSelectAllToggled"));
            }

            CommandBarSelectAllToggleOmitOnce = false;
        }
    }

    private bool m_IsCommandBarFavoriteEnabled = false;
    public bool IsCommandBarFavoriteEnabled
    {
        get => m_IsCommandBarFavoriteEnabled;
        set
        {
            m_IsCommandBarFavoriteEnabled = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("IsCommandBarFavoriteEnabled"));
        }
    }

    private bool m_IsCommandBarUnFavoriteEnabled = false;
    public bool IsCommandBarUnFavoriteEnabled
    {
        get => m_IsCommandBarUnFavoriteEnabled;
        set
        {
            m_IsCommandBarUnFavoriteEnabled = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("IsCommandBarUnFavoriteEnabled"));
        }
    }

    private bool m_IsCommandBarHideEnabled = false;
    public bool IsCommandBarHideEnabled
    {
        get => m_IsCommandBarHideEnabled;
        set
        {
            m_IsCommandBarHideEnabled = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("IsCommandBarHideEnabled"));
        }
    }

    private bool m_IsCommandBarUnHideEnabled = false;
    public bool IsCommandBarUnHideEnabled
    {
        get => m_IsCommandBarUnHideEnabled;
        set
        {
            m_IsCommandBarUnHideEnabled = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("IsCommandBarUnHideEnabled"));
        }
    }

    private bool _isCommandBarMarkAsReadEnabled = false;
    public bool IsCommandBarMarkAsReadEnabled
    {
        get => _isCommandBarMarkAsReadEnabled;
        set
        {
            _isCommandBarMarkAsReadEnabled = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("IsCommandBarMarkAsReadEnabled"));
        }
    }

    private bool _isCommandBarMarkAsUnreadEnabled = false;
    public bool IsCommandBarMarkAsUnreadEnabled
    {
        get => _isCommandBarMarkAsUnreadEnabled;
        set
        {
            _isCommandBarMarkAsUnreadEnabled = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("IsCommandBarMarkAsUnreadEnabled"));
        }
    }

    public Action<bool> OnCommandBarSelectAllToggleChanged;
}
