// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;

using ComicReader.Common;
using ComicReader.ViewModels;

using Microsoft.UI.Xaml.Controls;

namespace ComicReader.Helpers.MenuFlyoutHelpers;

internal static class ComicItemMenuFlyoutCreator
{
    public static List<MenuFlyoutItemBase> CreateMenuItems(ComicItemViewModel model, IComicItemMenuFlyoutHandler handler)
    {
        List<MenuFlyoutItemBase> result = [];
        {
            MenuFlyoutItem item = new()
            {
                Text = StringResourceProvider.Instance.OpenInNewTab,
                Icon = new FontIcon
                {
                    Glyph = "\uE8A5"
                }
            };
            item.Click += handler.OnOpenInNewTabClicked;
            result.Add(item);
        }
        if (!model.IsFavorite)
        {
            MenuFlyoutItem item = new()
            {
                Text = StringResourceProvider.Instance.AddToFavorites,
                Icon = new FontIcon
                {
                    Glyph = "\uE734"
                }
            };
            item.Click += handler.OnAddToFavoritesClicked;
            result.Add(item);
        }
        if (model.IsFavorite)
        {
            MenuFlyoutItem item = new()
            {
                Text = StringResourceProvider.Instance.RemoveFromFavorites,
                Icon = new FontIcon
                {
                    Glyph = "\uE8D9"
                }
            };
            item.Click += handler.OnRemoveFromFavoritesClicked;
            result.Add(item);
        }
        {
            MenuFlyoutSubItem groupItem = new()
            {
                Text = StringResourceProvider.Instance.SetCompletionState,
                Icon = new FontIcon
                {
                    Glyph = "\uE7C1"
                }
            };
            if (model.Comic.CompletionState != Data.Models.Comic.ComicData.CompletionStateEnum.NotStarted)
            {
                MenuFlyoutItem item = new()
                {
                    Text = StringResourceProvider.Instance.MarkAsUnread,
                };
                item.Click += handler.OnMarkAsUnreadClicked;
                groupItem.Items.Add(item);
            }
            if (model.Comic.CompletionState != Data.Models.Comic.ComicData.CompletionStateEnum.Started)
            {
                MenuFlyoutItem item = new()
                {
                    Text = StringResourceProvider.Instance.MarkAsReading,
                };
                item.Click += handler.OnMarkAsReadingClicked;
                groupItem.Items.Add(item);
            }
            if (model.Comic.CompletionState != Data.Models.Comic.ComicData.CompletionStateEnum.Completed)
            {
                MenuFlyoutItem item = new()
                {
                    Text = StringResourceProvider.Instance.MarkAsRead,
                };
                item.Click += handler.OnMarkAsReadClicked;
                groupItem.Items.Add(item);
            }
            result.Add(groupItem);
        }
        if (!model.IsHide)
        {
            MenuFlyoutItem item = new()
            {
                Text = StringResourceProvider.Instance.Hide,
                Icon = new FontIcon
                {
                    Glyph = "\uE8FF"
                }
            };
            item.Click += handler.OnHideClicked;
            result.Add(item);
        }
        if (model.IsHide)
        {
            MenuFlyoutItem item = new()
            {
                Text = StringResourceProvider.Instance.Unhide,
                Icon = new FontIcon
                {
                    Glyph = "\uE7B3"
                }
            };
            item.Click += handler.OnUnhideClicked;
            result.Add(item);
        }
        result.Add(new MenuFlyoutSeparator());
        {
            MenuFlyoutItem item = new()
            {
                Text = StringResourceProvider.Instance.Select,
                Icon = new FontIcon
                {
                    Glyph = "\uE762"
                }
            };
            item.Click += handler.OnSelectClicked;
            result.Add(item);
        }
        return result;
    }
}
