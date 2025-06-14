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
        PropertyTypeEnum.Tag => StringResourceProvider.Tag,
        _ => "",
    };

    public string DisplayName => Type switch
    {
        PropertyTypeEnum.Title => StringResourceProvider.Title,
        PropertyTypeEnum.Progress => StringResourceProvider.Progress,
        PropertyTypeEnum.Tag => Name,
        PropertyTypeEnum.Rating => StringResourceProvider.Rating,
        PropertyTypeEnum.CompletionState => StringResourceProvider.CompletionState,
        PropertyTypeEnum.LastReadTime => StringResourceProvider.LastReadTime,
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

    public IComparable GetPropertyAsComparable(ComicModel? comic)
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
            PropertyTypeEnum.Title => comic.Title ?? StringResourceProvider.Untitled,
            PropertyTypeEnum.Progress => comic.Progress,
            PropertyTypeEnum.Tag => GetConcatenatedTag(),
            PropertyTypeEnum.Rating => comic.Rating,
            PropertyTypeEnum.CompletionState => CompletionStateToComparable(comic.CompletionState),
            PropertyTypeEnum.LastReadTime => comic.LastVisit.Ticks,
            _ => comic.Id
        };
    }

    public IEnumerable<IGroupInfo> GetPropertyAsGroupInfos(ComicModel? comic)
    {
        if (comic == null)
        {
            return [];
        }

        GroupInfo<string> GetTitleGroup(string title)
        {
            title = title.TrimStart();
            if (string.IsNullOrEmpty(title))
            {
                return GroupInfo<string>.New(StringResourceProvider.Untitled);
            }
            return GroupInfo<string>.New(title[0].ToString().ToUpper());
        }

        GroupInfo<int> GetProgressGroup(int progress)
        {
            progress = Math.Min(Math.Max(progress, 0), 100);
            if (progress < 10)
            {
                return GroupInfo<int>.New("<10%", 0);
            }
            if (progress >= 100)
            {
                return GroupInfo<int>.New("100%", 100);
            }
            return GroupInfo<int>.New($"{progress / 10 * 10}%", progress);
        }

        IEnumerable<GroupInfo<string>> GetTagGroups()
        {
            ComicData.TagData? tagData = comic.Tags.FirstOrDefault(tag => tag.Name == Name);
            if (tagData == null || tagData.Tags.Count == 0)
            {
                return [GroupInfo<string>.New(StringResourceProvider.Ungrouped)];
            }
            List<GroupInfo<string>> groups = new(tagData.Tags.Count);
            foreach (string tag in tagData.Tags)
            {
                groups.Add(GroupInfo<string>.New(tag));
            }
            return groups;
        }

        GroupInfo<int> GetRatingGroup()
        {
            int rating = comic.Rating;
            if (rating >= 5)
            {
                return GroupInfo<int>.New("5", 5);
            }
            if (rating <= 0)
            {
                return GroupInfo<int>.New(StringResourceProvider.NoRating, 0);
            }
            return GroupInfo<int>.New(rating.ToString(), rating);
        }

        GroupInfo<int> GetCompletionStateGroup(ComicData.CompletionStateEnum state)
        {
            return state switch
            {
                ComicData.CompletionStateEnum.Completed => GroupInfo<int>.New(StringResourceProvider.Finished, 3),
                ComicData.CompletionStateEnum.Started => GroupInfo<int>.New(StringResourceProvider.Reading, 2),
                ComicData.CompletionStateEnum.NotStarted => GroupInfo<int>.New(StringResourceProvider.Unread, 1),
                _ => GroupInfo<int>.New(StringResourceProvider.Ungrouped, 0), // Unknown state
            };
        }

        GroupInfo<long> GetLastReadTimeGroup(DateTimeOffset lastReadTime)
        {
            if (lastReadTime == DateTimeOffset.MinValue)
            {
                return GroupInfo<long>.New(StringResourceProvider.Ungrouped, 0L);
            }
            return GroupInfo<long>.New(lastReadTime.ToString("D"), lastReadTime.Ticks);
        }

        return Type switch
        {
            PropertyTypeEnum.Title => [GetTitleGroup(comic.Title)],
            PropertyTypeEnum.Progress => [GetProgressGroup(comic.Progress)],
            PropertyTypeEnum.Tag => GetTagGroups().Cast<IGroupInfo>(),
            PropertyTypeEnum.Rating => [GetRatingGroup()],
            PropertyTypeEnum.CompletionState => [GetCompletionStateGroup(comic.CompletionState)],
            PropertyTypeEnum.LastReadTime => [GetLastReadTimeGroup(comic.LastVisit)],
            _ => [GroupInfo<string>.New(StringResourceProvider.Ungrouped)]
        };
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

    public interface IGroupInfo
    {
        public string Name { get; }
        public IComparable SortKey { get; }
    }

    private readonly struct GroupInfo<T> : IGroupInfo where T : IComparable
    {
        private readonly string _name;
        private readonly T _sortKey;

        private GroupInfo(string name, T sortKey)
        {
            _name = name;
            _sortKey = sortKey;
        }

        readonly string IGroupInfo.Name => _name;
        readonly IComparable IGroupInfo.SortKey => _sortKey;

        public static GroupInfo<T> New(string name, T sortKey)
        {
            return new GroupInfo<T>(name, sortKey);
        }

        public static GroupInfo<string> New(string name)
        {
            return new GroupInfo<string>(name, name);
        }
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
