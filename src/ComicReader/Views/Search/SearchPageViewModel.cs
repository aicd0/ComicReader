﻿// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;

using ComicReader.Common.Lifecycle;
using ComicReader.Common.Threading;
using ComicReader.Data.Models;
using ComicReader.Data.Models.Comic;
using ComicReader.SDK.Common.Threading;
using ComicReader.UserControls.ComicItemView;
using ComicReader.ViewModels;

using Microsoft.UI.Xaml.Controls;

namespace ComicReader.Views.Search;

internal partial class SearchPageViewModel : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    private readonly ITaskDispatcher _sharedDispatcher = TaskDispatcher.DefaultQueue;
    private readonly List<ComicItemViewModel> _selectedItems = [];

    public bool IsLoading;

    public MutableLiveData<bool> UpdateSearchResultLiveDate = new();
    public bool IsResultEmpty => SearchResults.Count == 0;

    public ObservableCollection<ComicItemViewModel> SearchResults = [];

    private bool m_IsLoadingRingVisible;
    public bool IsLoadingRingVisible
    {
        get => m_IsLoadingRingVisible;
        set
        {
            m_IsLoadingRingVisible = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsLoadingRingVisible)));
        }
    }

    private string _title = "";
    public string Title
    {
        get => _title;
        set
        {
            _title = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Title)));
        }
    }

    private string _filterDetails = "";
    public string FilterDetails
    {
        get => _filterDetails;
        set
        {
            _filterDetails = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(FilterDetails)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(FilterDetailsVisible)));
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
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsResultGridVisible)));
        }
    }

    private string _noResultText = string.Empty;
    public string NoResultText
    {
        get => _noResultText;
        set
        {
            _noResultText = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(NoResultText)));
        }
    }

    private bool m_IsNoResultTextVisible;
    public bool IsNoResultTextVisible
    {
        get => m_IsNoResultTextVisible;
        set
        {
            m_IsNoResultTextVisible = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsNoResultTextVisible)));
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

    private bool _isCommandBarSelectAllToggled = false;
    public bool IsCommandBarSelectAllToggled
    {
        get => _isCommandBarSelectAllToggled;
        set
        {
            _isCommandBarSelectAllToggled = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsCommandBarSelectAllToggled)));
        }
    }

    private bool m_IsCommandBarFavoriteEnabled = false;
    public bool IsCommandBarFavoriteEnabled
    {
        get => m_IsCommandBarFavoriteEnabled;
        set
        {
            m_IsCommandBarFavoriteEnabled = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsCommandBarFavoriteEnabled)));
        }
    }

    private bool m_IsCommandBarUnFavoriteEnabled = false;
    public bool IsCommandBarUnFavoriteEnabled
    {
        get => m_IsCommandBarUnFavoriteEnabled;
        set
        {
            m_IsCommandBarUnFavoriteEnabled = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsCommandBarUnFavoriteEnabled)));
        }
    }

    private bool m_IsCommandBarHideEnabled = false;
    public bool IsCommandBarHideEnabled
    {
        get => m_IsCommandBarHideEnabled;
        set
        {
            m_IsCommandBarHideEnabled = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsCommandBarHideEnabled)));
        }
    }

    private bool m_IsCommandBarUnHideEnabled = false;
    public bool IsCommandBarUnHideEnabled
    {
        get => m_IsCommandBarUnHideEnabled;
        set
        {
            m_IsCommandBarUnHideEnabled = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsCommandBarUnHideEnabled)));
        }
    }

    private bool _isCommandBarMarkAsReadEnabled = false;
    public bool IsCommandBarMarkAsReadEnabled
    {
        get => _isCommandBarMarkAsReadEnabled;
        set
        {
            _isCommandBarMarkAsReadEnabled = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsCommandBarMarkAsReadEnabled)));
        }
    }

    private bool _isCommandBarMarkAsReadingEnabled = false;
    public bool IsCommandBarMarkAsReadingEnabled
    {
        get => _isCommandBarMarkAsReadingEnabled;
        set
        {
            _isCommandBarMarkAsReadingEnabled = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsCommandBarMarkAsReadingEnabled)));
        }
    }

    private bool _isCommandBarMarkAsUnreadEnabled = false;
    public bool IsCommandBarMarkAsUnreadEnabled
    {
        get => _isCommandBarMarkAsUnreadEnabled;
        set
        {
            _isCommandBarMarkAsUnreadEnabled = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsCommandBarMarkAsUnreadEnabled)));
        }
    }

    public void UpdateUI()
    {
        IsLoadingRingVisible = IsLoading;
        IsResultGridVisible = !IsLoading && !IsResultEmpty;
        IsNoResultTextVisible = !IsLoading && IsResultEmpty;
    }

    public void UpdateComicSelection(IEnumerable<ComicItemViewModel> selectedItems)
    {
        List<ComicItemViewModel> selectedItemsCopy = [.. selectedItems];
        _sharedDispatcher.Submit("UpdateComicSelection", () =>
        {
            _selectedItems.Clear();
            _selectedItems.AddRange(selectedItemsCopy);
            UpdateCommandBarButtonStates();
        });
    }

    public void ApplyOperationToComic(ComicOperationType operationType, ComicItemViewModel item)
    {
        _sharedDispatcher.Submit("ApplyOperationToComic", () =>
        {
            BatchApplyOperation(operationType, [item]);
            UpdateCommandBarButtonStates();
        });
    }

    public void ApplyOperationToComicSelection(ComicOperationType operationType)
    {
        _sharedDispatcher.Submit("ApplyOperationToComicSelection", () =>
        {
            BatchApplyOperation(operationType, _selectedItems);
            UpdateCommandBarButtonStates();
        });
    }

    private void UpdateCommandBarButtonStates()
    {
        bool allSelected = _selectedItems.Count == SearchResults.Count;
        bool favoriteEnabled = false;
        bool unfavoriteEnabled = false;
        bool hideEnabled = false;
        bool unhideEnabled = false;
        bool markAsReadEnabled = false;
        bool markAsReadingEnabled = false;
        bool markAsUnreadEnabled = false;

        foreach (ComicItemViewModel item in _selectedItems)
        {
            if (item.IsFavorite)
            {
                unfavoriteEnabled = true;
            }
            else
            {
                favoriteEnabled = true;
            }

            if (item.IsHide)
            {
                unhideEnabled = true;
            }
            else
            {
                hideEnabled = true;
            }

            if (!item.IsRead)
            {
                markAsReadEnabled = true;
            }

            if (!item.IsReading)
            {
                markAsReadingEnabled = true;
            }

            if (!item.IsUnread)
            {
                markAsUnreadEnabled = true;
            }
        }

        _ = MainThreadUtils.RunInMainThread(delegate
        {
            IsCommandBarSelectAllToggled = allSelected;
            IsCommandBarFavoriteEnabled = favoriteEnabled;
            IsCommandBarUnFavoriteEnabled = unfavoriteEnabled;
            IsCommandBarHideEnabled = hideEnabled;
            IsCommandBarUnHideEnabled = unhideEnabled;
            IsCommandBarMarkAsReadEnabled = markAsReadEnabled;
            IsCommandBarMarkAsReadingEnabled = markAsReadingEnabled;
            IsCommandBarMarkAsUnreadEnabled = markAsUnreadEnabled;
        });
    }

    private void BatchApplyOperation(ComicOperationType operationType, List<ComicItemViewModel> models)
    {
        switch (operationType)
        {
            case ComicOperationType.Favorite:
                {
                    List<ComicItemViewModel> items = models.FindAll(x => !x.IsFavorite);
                    FavoriteModel.Instance.BatchAdd(items.ConvertAll(x => new FavoriteModel.FavoriteItem
                    {
                        Id = x.Comic.Id,
                        Title = x.Comic.Title,
                    }));
                    ModifyExistingItems(items, (item) => { item.IsFavorite = true; });
                }
                break;
            case ComicOperationType.Unfavorite:
                {
                    List<ComicItemViewModel> items = models.FindAll(x => x.IsFavorite);
                    FavoriteModel.Instance.BatchRemoveWithId(items.ConvertAll(x => x.Comic.Id));
                    ModifyExistingItems(items, (item) => { item.IsFavorite = false; });
                }
                break;
            case ComicOperationType.Hide:
                {
                    List<ComicItemViewModel> items = models.FindAll(x => !x.IsHide);
                    foreach (ComicItemViewModel item in items)
                    {
                        item.Comic.SaveHiddenAsync(true).Wait();
                    }
                    UpdateSearchResultLiveDate.Emit(true);
                }
                break;
            case ComicOperationType.Unhide:
                {
                    List<ComicItemViewModel> items = models.FindAll(x => x.IsHide);
                    foreach (ComicItemViewModel item in items)
                    {
                        item.Comic.SaveHiddenAsync(false).Wait();
                    }
                    UpdateSearchResultLiveDate.Emit(true);
                }
                break;
            case ComicOperationType.MarkAsRead:
                {
                    List<ComicItemViewModel> items = models.FindAll(x => !x.IsRead);
                    foreach (ComicItemViewModel item in items)
                    {
                        item.Comic.SetCompletionStateToCompleted().Wait();
                    }
                    ModifyExistingItems(items, (item) =>
                    {
                        item.CompletionState = ComicData.CompletionStateEnum.Completed;
                        item.UpdateProgress(true);
                    });
                }
                break;
            case ComicOperationType.MarkAsReading:
                {
                    List<ComicItemViewModel> items = models.FindAll(x => !x.IsReading);
                    foreach (ComicItemViewModel item in items)
                    {
                        item.Comic.SetCompletionStateToStarted().Wait();
                    }
                    ModifyExistingItems(items, (item) =>
                    {
                        item.CompletionState = ComicData.CompletionStateEnum.Started;
                        item.UpdateProgress(true);
                    });
                }
                break;
            case ComicOperationType.MarkAsUnread:
                {
                    List<ComicItemViewModel> items = models.FindAll(x => !x.IsUnread);
                    foreach (ComicItemViewModel item in items)
                    {
                        item.Comic.SetCompletionStateToNotStarted().Wait();
                    }
                    ModifyExistingItems(items, (item) =>
                    {
                        item.CompletionState = ComicData.CompletionStateEnum.NotStarted;
                        item.UpdateProgress(true);
                    });
                }
                break;
            default:
                break;
        }
    }

    private void ModifyExistingItems(IEnumerable<ComicItemViewModel> items, Action<ComicItemViewModel> action)
    {
        Dictionary<ComicModel, ComicItemViewModel> changedItems = [];
        foreach (ComicItemViewModel item in items)
        {
            ComicItemViewModel newItem = new(item);
            action(newItem);
            changedItems[item.Comic] = newItem;
        }
        ModifyExistingList(_selectedItems, changedItems);
        _ = MainThreadUtils.RunInMainThread(delegate
        {
            ModifyExistingList(SearchResults, changedItems);
        });
    }

    private void ModifyExistingList(IList<ComicItemViewModel> list, IReadOnlyDictionary<ComicModel, ComicItemViewModel> changedItems)
    {
        for (int i = 0; i < list.Count; i++)
        {
            ComicItemViewModel oldItem = list[i];
            if (changedItems.TryGetValue(oldItem.Comic, out ComicItemViewModel? newItem))
            {
                list[i] = newItem;
            }
        }
    }
}
