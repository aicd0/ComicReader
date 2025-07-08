// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

using ComicReader.Common;
using ComicReader.Data.Models.Comic;
using ComicReader.SDK.Common.DebugTools;

namespace ComicReader.Data.Models;

internal class ComicPropertyModel
{
    private const string TAG = nameof(ComicPropertyModel);
    private const string PROP_TYPE_TITLE = "Title";
    private const string PROP_TYPE_PROGRESS = "Progress";
    private const string PROP_TYPE_TAG = "Tag";
    private const string PROP_TYPE_RATING = "Rating";
    private const string PROP_TYPE_COMPLETION_STATE = "CompletionState";
    private const string PROP_TYPE_LAST_READ_TIME = "LastReadTime";

    private static readonly List<PropertyTypeEnum> _properties = [
        PropertyTypeEnum.Title,
        PropertyTypeEnum.Progress,
        PropertyTypeEnum.Rating,
        PropertyTypeEnum.CompletionState,
        PropertyTypeEnum.LastReadTime,
    ];

    private PropertyTypeEnum Type { get; set; } = PropertyTypeEnum.Title;
    private string Name { get; set; } = "";

    public string DisplayGroupName => Type switch
    {
        PropertyTypeEnum.Tag => StringResourceProvider.Instance.Tag,
        _ => "",
    };

    public string DisplayName => Type switch
    {
        PropertyTypeEnum.Title => StringResourceProvider.Instance.Title,
        PropertyTypeEnum.Progress => StringResourceProvider.Instance.Progress,
        PropertyTypeEnum.Tag => Name,
        PropertyTypeEnum.Rating => StringResourceProvider.Instance.Rating,
        PropertyTypeEnum.CompletionState => StringResourceProvider.Instance.CompletionState,
        PropertyTypeEnum.LastReadTime => StringResourceProvider.Instance.LastReadTime,
        _ => "",
    };

    public override bool Equals(object? obj)
    {
        if (obj is ComicPropertyModel other)
        {
            return Type == other.Type && Name == other.Name;
        }
        return false;
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(Type, Name);
    }

    public JsonNode? ToJson()
    {
        var jsonModel = new JsonModel
        {
            Type = PropertyTypeToString(Type),
            Name = Name
        };
        return JsonSerializer.SerializeToNode(jsonModel);
    }

    public List<T> SortComics<T>(IEnumerable<T> items, Func<T, ComicModel> selector, bool ascending)
    {
        IComicItemSorter sorter = GetComicItemSorter();
        return sorter.SortComics(items, selector, ascending);
    }

    public List<GroupItem<T>> GroupComics<T>(IEnumerable<T> items, Func<T, ComicModel> selector, bool ascending)
    {
        IComicGroupSorter sorter = GetComicGroupSorter();
        return sorter.GroupComics(items, selector, ascending);
    }

    private IComicItemSorter GetComicItemSorter()
    {
        string GetConcatenatedTag(ComicModel comic)
        {
            ComicData.TagData? tagData = comic.Tags.FirstOrDefault(tag => tag.Name == Name);
            if (tagData == null)
            {
                return string.Empty;
            }
            List<string> tags = [.. tagData.Tags];
            tags.Sort(StringComparer.OrdinalIgnoreCase);
            return string.Join(' ', tags);
        }

        int CompletionStateToComparable(ComicData.CompletionStateEnum state)
        {
            return state switch
            {
                ComicData.CompletionStateEnum.Completed => 100,
                ComicData.CompletionStateEnum.Started => 50,
                ComicData.CompletionStateEnum.NotStarted => 0,
                _ => -1, // Unknown state
            };
        }

        return Type switch
        {
            PropertyTypeEnum.Title => new ComicItemSorter<List<string>>(
                (x) => StringUtils.SmartFileNameKeySelector(x.Title ?? StringResourceProvider.Instance.Untitled), StringUtils.SmartFileNameComparer),
            PropertyTypeEnum.Progress => new ComicItemSorter<int>((x) => x.Progress),
            PropertyTypeEnum.Tag => new ComicItemSorter<List<string>>(
                (x) => StringUtils.SmartFileNameKeySelector(GetConcatenatedTag(x)), StringUtils.SmartFileNameComparer),
            PropertyTypeEnum.Rating => new ComicItemSorter<int>((x) => x.Rating),
            PropertyTypeEnum.CompletionState => new ComicItemSorter<int>((x) => CompletionStateToComparable(x.CompletionState)),
            PropertyTypeEnum.LastReadTime => new ComicItemSorter<long>((x) => x.LastVisit.Ticks),
            _ => new ComicItemSorter<long>((x) => x.Id)
        };
    }

    private IComicGroupSorter GetComicGroupSorter()
    {
        GroupInfo<List<string>> GetTitleGroup(string title)
        {
            title = title.TrimStart();
            string name;
            if (string.IsNullOrEmpty(title))
            {
                name = StringResourceProvider.Instance.Untitled;
            }
            else
            {
                name = title[0].ToString().ToUpper();
            }
            return new(name, StringUtils.SmartFileNameKeySelector(name));
        }

        GroupInfo<int> GetProgressGroup(int progress)
        {
            progress = Math.Min(Math.Max(progress, 0), 100);
            if (progress < 10)
            {
                return new("<10%", 0);
            }
            if (progress >= 100)
            {
                return new("100%", 100);
            }
            return new($"{progress / 10 * 10}%", progress);
        }

        IEnumerable<GroupInfo<List<string>>> GetTagGroups(ComicModel comic)
        {
            ComicData.TagData? tagData = comic.Tags.FirstOrDefault(tag => tag.Name == Name);
            if (tagData == null || tagData.Tags.Count == 0)
            {
                string name = StringResourceProvider.Instance.Ungrouped;
                return [new(name, StringUtils.SmartFileNameKeySelector(name))];
            }
            List<GroupInfo<List<string>>> groups = new(tagData.Tags.Count);
            foreach (string tag in tagData.Tags)
            {
                groups.Add(new(tag, StringUtils.SmartFileNameKeySelector(tag)));
            }
            return groups;
        }

        GroupInfo<int> GetRatingGroup(ComicModel comic)
        {
            int rating = comic.Rating;
            if (rating >= 5)
            {
                return new("5", 5);
            }
            if (rating <= 0)
            {
                return new(StringResourceProvider.Instance.NoRating, 0);
            }
            return new(rating.ToString(), rating);
        }

        GroupInfo<int> GetCompletionStateGroup(ComicData.CompletionStateEnum state)
        {
            return state switch
            {
                ComicData.CompletionStateEnum.Completed => new(StringResourceProvider.Instance.Finished, 3),
                ComicData.CompletionStateEnum.Started => new(StringResourceProvider.Instance.Reading, 2),
                ComicData.CompletionStateEnum.NotStarted => new(StringResourceProvider.Instance.Unread, 1),
                _ => new(StringResourceProvider.Instance.Ungrouped, 0), // Unknown state
            };
        }

        GroupInfo<long> GetLastReadTimeGroup(DateTimeOffset lastReadTime)
        {
            if (lastReadTime == DateTimeOffset.MinValue)
            {
                return new(StringResourceProvider.Instance.Ungrouped, 0L);
            }
            return new(lastReadTime.ToString("D"), lastReadTime.Ticks);
        }

        return Type switch
        {
            PropertyTypeEnum.Title => new ComicGroupSorter<List<string>>((x) => [GetTitleGroup(x.Title)], StringUtils.SmartFileNameComparer),
            PropertyTypeEnum.Progress => new ComicGroupSorter<int>((x) => [GetProgressGroup(x.Progress)]),
            PropertyTypeEnum.Tag => new ComicGroupSorter<List<string>>(GetTagGroups, StringUtils.SmartFileNameComparer),
            PropertyTypeEnum.Rating => new ComicGroupSorter<int>((x) => [GetRatingGroup(x)]),
            PropertyTypeEnum.CompletionState => new ComicGroupSorter<int>((x) => [GetCompletionStateGroup(x.CompletionState)]),
            PropertyTypeEnum.LastReadTime => new ComicGroupSorter<long>((x) => [GetLastReadTimeGroup(x.LastVisit)]),
            _ => new ComicGroupSorter<int>((x) => [new(StringResourceProvider.Instance.Ungrouped, 0)])
        };
    }

    public static ComicPropertyModel? FromJson(JsonNode? model)
    {
        if (model == null)
        {
            return null;
        }

        JsonModel? jsonModel = null;
        try
        {
            jsonModel = JsonSerializer.Deserialize<JsonModel>(model);
        }
        catch (JsonException e)
        {
            Logger.F(TAG, "FromJson", e);
        }
        if (jsonModel == null)
        {
            return null;
        }

        return new ComicPropertyModel
        {
            Type = StringToPropertyType(jsonModel.Type),
            Name = jsonModel.Name ?? "",
        };
    }

    private static string PropertyTypeToString(PropertyTypeEnum value)
    {
        return value switch
        {
            PropertyTypeEnum.Title => PROP_TYPE_TITLE,
            PropertyTypeEnum.Progress => PROP_TYPE_PROGRESS,
            PropertyTypeEnum.Tag => PROP_TYPE_TAG,
            PropertyTypeEnum.Rating => PROP_TYPE_RATING,
            PropertyTypeEnum.CompletionState => PROP_TYPE_COMPLETION_STATE,
            PropertyTypeEnum.LastReadTime => PROP_TYPE_LAST_READ_TIME,
            _ => PROP_TYPE_TITLE,
        };
    }

    private static PropertyTypeEnum StringToPropertyType(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return PropertyTypeEnum.Title;
        }

        return value switch
        {
            PROP_TYPE_TITLE => PropertyTypeEnum.Title,
            PROP_TYPE_PROGRESS => PropertyTypeEnum.Progress,
            PROP_TYPE_TAG => PropertyTypeEnum.Tag,
            PROP_TYPE_RATING => PropertyTypeEnum.Rating,
            PROP_TYPE_COMPLETION_STATE => PropertyTypeEnum.CompletionState,
            PROP_TYPE_LAST_READ_TIME => PropertyTypeEnum.LastReadTime,
            _ => PropertyTypeEnum.Title,
        };
    }

    public static async Task<List<ComicPropertyModel>> GetProperties()
    {
        var properties = new List<ComicPropertyModel>();
        foreach (PropertyTypeEnum propertyType in _properties)
        {
            properties.Add(new ComicPropertyModel
            {
                Type = propertyType,
            });
        }

        {
            List<string> tagCategories = await ComicModel.GetAllTagCategories();
            foreach (string tag in tagCategories)
            {
                properties.Add(new ComicPropertyModel
                {
                    Type = PropertyTypeEnum.Tag,
                    Name = tag,
                });
            }
        }

        return properties;
    }

    private class JsonModel
    {
        [JsonPropertyName("Type")]
        public string? Type { get; set; }

        [JsonPropertyName("Name")]
        public string? Name { get; set; }
    }

    public class GroupItem<T>(List<T> items, string name)
    {
        public List<T> Items { get; } = items;
        public string Name { get; } = name;
    }

    private interface IComicItemSorter
    {
        List<T> SortComics<T>(IEnumerable<T> items, Func<T, ComicModel> selector, bool ascending);
    }

    private class ComicItemSorter<K>(Func<ComicModel, K> keySelector, IComparer<K>? keyComparer = null) : IComicItemSorter
    {
        public Func<ComicModel, K> ItemSortKeySelector = keySelector;
        public IComparer<K> ItemComparer = keyComparer ?? Comparer<K>.Default;

        public List<T> SortComics<T>(IEnumerable<T> items, Func<T, ComicModel> selector, bool ascending)
        {
            if (ascending)
            {
                return [.. items.OrderBy(x => ItemSortKeySelector(selector(x)), ItemComparer)];
            }
            else
            {
                return [.. items.OrderByDescending(x => ItemSortKeySelector(selector(x)), ItemComparer)];
            }
        }
    }

    private interface IComicGroupSorter
    {
        List<GroupItem<T>> GroupComics<T>(IEnumerable<T> items, Func<T, ComicModel> selector, bool ascending);
    }

    private class ComicGroupSorter<K>(Func<ComicModel, IEnumerable<GroupInfo<K>>> groupInfoSelector, IComparer<K>? keyComparer = null) : IComicGroupSorter
    {
        public Func<ComicModel, IEnumerable<GroupInfo<K>>> GroupInfoSelector = groupInfoSelector;
        public IComparer<K> GroupComparer = keyComparer ?? Comparer<K>.Default;

        public List<GroupItem<T>> GroupComics<T>(IEnumerable<T> items, Func<T, ComicModel> selector, bool ascending)
        {
            Dictionary<string, List<T>> groupMap = [];
            Dictionary<string, GroupInfo<K>> groupInfoMap = [];
            foreach (T item in items)
            {
                IEnumerable<GroupInfo<K>> groupInfos = GroupInfoSelector(selector(item));
                foreach (GroupInfo<K> groupInfo in groupInfos)
                {
                    if (!groupMap.TryGetValue(groupInfo.Name, out List<T>? group))
                    {
                        group = [];
                        groupMap[groupInfo.Name] = group;
                    }
                    group.Add(item);
                    groupInfoMap[groupInfo.Name] = groupInfo;
                }
            }
            List<GroupItem<T>> comicGroups = [];
            foreach (KeyValuePair<string, List<T>> p in groupMap)
            {
                comicGroups.Add(new(p.Value, p.Key));
            }
            if (ascending)
            {
                return [.. comicGroups.OrderBy(x => groupInfoMap[x.Name].SortKey, GroupComparer)];
            }
            else
            {
                return [.. comicGroups.OrderByDescending(x => groupInfoMap[x.Name].SortKey, GroupComparer)];
            }
        }
    }

    private class GroupInfo<T>(string name, T sortKey)
    {
        public string Name { get; } = name;
        public T SortKey { get; } = sortKey;
    }

    private enum PropertyTypeEnum
    {
        CompletionState,
        Progress,
        Rating,
        Tag,
        Title,
        LastReadTime,
    }
}
