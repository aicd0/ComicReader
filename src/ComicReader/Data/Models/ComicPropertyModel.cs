// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

using ComicReader.Data.Models.Comic;
using ComicReader.Data.SqlHelpers;
using ComicReader.Data.Tables;
using ComicReader.SDK.Common.DebugTools;

namespace ComicReader.Data.Models;

internal class ComicPropertyModel
{
    private const string TAG = nameof(ComicPropertyModel);
    private const string PROP_TYPE_TITLE = "Title";
    private const string PROP_TYPE_PROGRESS = "Progress";
    private const string PROP_TYPE_TAG = "Tag";

    private static readonly List<PropertyTypeEnum> _properties = [
        PropertyTypeEnum.Title,
        PropertyTypeEnum.Progress,
    ];

    private PropertyTypeEnum Type { get; set; } = PropertyTypeEnum.Title;
    private string Name { get; set; } = "";

    public string DisplayGroupName => Type switch
    {
        PropertyTypeEnum.Tag => "Tag",
        _ => "",
    };

    public string DisplayName => Type switch
    {
        PropertyTypeEnum.Title => "Title",
        PropertyTypeEnum.Progress => "Progress",
        PropertyTypeEnum.Tag => Name,
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
        return Type switch
        {
            PropertyTypeEnum.Title => comic.Title ?? "",
            PropertyTypeEnum.Progress => comic.Progress,
            _ => comic.Id
        };
    }

    public string GetPropertyAsGroupName(ComicModel? comic)
    {
        if (comic == null)
        {
            return "";
        }

        string GetFirstLetter(string? value)
        {
            value ??= string.Empty;
            value = value.TrimStart();
            if (string.IsNullOrEmpty(value))
            {
                return "";
            }
            return value.ToUpper()[0].ToString();
        }

        return Type switch
        {
            PropertyTypeEnum.Title => GetFirstLetter(comic.Title),
            PropertyTypeEnum.Progress => (comic.Progress / 10 * 10).ToString(),
            _ => ""
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
            HashSet<string> tags = [];
            var command = new SelectCommand<TagCategoryTable>(TagCategoryTable.Instance);
            SelectCommand<TagCategoryTable>.IToken<string> nameToken = command.PutQueryString(TagCategoryTable.ColumnName);
            using SelectCommand<TagCategoryTable>.IReader reader = await command.ExecuteAsync();
            while (reader.Read())
            {
                string name = nameToken.GetValue();
                tags.Add(name);
            }
            foreach (string tag in tags)
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

    private enum PropertyTypeEnum
    {
        Title,
        Progress,
        Tag,
    }
}
