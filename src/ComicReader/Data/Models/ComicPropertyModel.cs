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
        var paired = items
            .Select(x => new KeyValuePair<IComparable, T>(GetPropertyAsComparable(selector(x)), x))
            .ToList();
        if (ascending)
        {
            paired.Sort((x, y) => x.Key.CompareTo(y.Key));
        }
        else
        {
            paired.Sort((x, y) => y.Key.CompareTo(x.Key));
        }
        return [.. paired.Select(x => x.Value)];
    }

    public List<GroupItem<T>> GroupComics<T>(IEnumerable<T> items, Func<T, ComicModel> selector, bool ascending)
    {
        Dictionary<string, List<T>> groupMap = [];
        Dictionary<string, GroupInfo> groupInfoMap = [];
        foreach (T item in items)
        {
            IEnumerable<GroupInfo> groupInfos = GetPropertyAsGroupInfos(selector(item));
            foreach (GroupInfo groupInfo in groupInfos)
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
            comicGroups.Sort((x, y) => groupInfoMap[x.Name].SortKey.CompareTo(groupInfoMap[y.Name].SortKey));
        }
        else
        {
            comicGroups.Sort((y, x) => groupInfoMap[x.Name].SortKey.CompareTo(groupInfoMap[y.Name].SortKey));
        }
        return comicGroups;
    }

    private IComparable GetPropertyAsComparable(ComicModel? comic)
    {
        if (comic == null)
        {
            return "";
        }

        string GetConcatenatedTag()
        {
            ComicData.TagData? tagData = comic.Tags.FirstOrDefault(tag => tag.Name == Name);
            if (tagData == null)
            {
                return string.Empty;
            }
            List<string> tags = [.. tagData.Tags];
            tags.Sort(StringComparer.OrdinalIgnoreCase);
            return string.Join(',', tags);
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
            PropertyTypeEnum.Title => comic.Title ?? StringResourceProvider.Instance.Untitled,
            PropertyTypeEnum.Progress => comic.Progress,
            PropertyTypeEnum.Tag => GetConcatenatedTag(),
            PropertyTypeEnum.Rating => comic.Rating,
            PropertyTypeEnum.CompletionState => CompletionStateToComparable(comic.CompletionState),
            PropertyTypeEnum.LastReadTime => comic.LastVisit.Ticks,
            _ => comic.Id
        };
    }

    private IEnumerable<GroupInfo> GetPropertyAsGroupInfos(ComicModel? comic)
    {
        if (comic == null)
        {
            return [];
        }

        GroupInfo GetTitleGroup(string title)
        {
            title = title.TrimStart();
            if (string.IsNullOrEmpty(title))
            {
                return new(StringResourceProvider.Instance.Untitled);
            }
            return new(title[0].ToString().ToUpper());
        }

        GroupInfo GetProgressGroup(int progress)
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

        IEnumerable<GroupInfo> GetTagGroups()
        {
            ComicData.TagData? tagData = comic.Tags.FirstOrDefault(tag => tag.Name == Name);
            if (tagData == null || tagData.Tags.Count == 0)
            {
                return [new(StringResourceProvider.Instance.Ungrouped)];
            }
            List<GroupInfo> groups = new(tagData.Tags.Count);
            foreach (string tag in tagData.Tags)
            {
                groups.Add(new(tag));
            }
            return groups;
        }

        GroupInfo GetRatingGroup()
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

        GroupInfo GetCompletionStateGroup(ComicData.CompletionStateEnum state)
        {
            return state switch
            {
                ComicData.CompletionStateEnum.Completed => new(StringResourceProvider.Instance.Finished, 3),
                ComicData.CompletionStateEnum.Started => new(StringResourceProvider.Instance.Reading, 2),
                ComicData.CompletionStateEnum.NotStarted => new(StringResourceProvider.Instance.Unread, 1),
                _ => new(StringResourceProvider.Instance.Ungrouped, 0), // Unknown state
            };
        }

        GroupInfo GetLastReadTimeGroup(DateTimeOffset lastReadTime)
        {
            if (lastReadTime == DateTimeOffset.MinValue)
            {
                return new(StringResourceProvider.Instance.Ungrouped, 0L);
            }
            return new(lastReadTime.ToString("D"), lastReadTime.Ticks);
        }

        return Type switch
        {
            PropertyTypeEnum.Title => [GetTitleGroup(comic.Title)],
            PropertyTypeEnum.Progress => [GetProgressGroup(comic.Progress)],
            PropertyTypeEnum.Tag => GetTagGroups(),
            PropertyTypeEnum.Rating => [GetRatingGroup()],
            PropertyTypeEnum.CompletionState => [GetCompletionStateGroup(comic.CompletionState)],
            PropertyTypeEnum.LastReadTime => [GetLastReadTimeGroup(comic.LastVisit)],
            _ => [new(StringResourceProvider.Instance.Ungrouped)]
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

    private class GroupInfo
    {
        public GroupInfo(string name)
        {
            Name = name;
            SortKey = name;
        }

        public GroupInfo(string name, IComparable sortKey)
        {
            Name = name;
            SortKey = sortKey;
        }

        public string Name { get; }
        public IComparable SortKey { get; }
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
