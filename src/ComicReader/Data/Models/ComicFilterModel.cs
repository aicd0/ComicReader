// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace ComicReader.Data.Models;

class ComicFilterModel : JsonDatabase<ComicFilterModel.JsonModel>
{
    public const string PROP_TYPE_TITLE = "Title";
    public const string PROP_TYPE_PROGRESS = "Progress";
    public const string PROP_TYPE_TAG = "Tag";
    public const string VIEW_TYPE_LARGE = "Large";
    public const string VIEW_TYPE_MEDIUM = "Medium";

    public static readonly ComicFilterModel Instance = new();

    private ComicFilterModel() : base("filters.json") { }

    protected override JsonModel CreateModel()
    {
        return new();
    }

    public async Task<ExternalModel?> GetModel()
    {
        return await Read(ExternalModel.From);
    }

    public async Task UpdateModel(ExternalModel model)
    {
        await Write(m =>
        {
            model.To(m);
            return true;
        });
        await Save();
    }

    public class JsonModel
    {
        [JsonPropertyName("LastFilter")]
        public FilterModel? LastFilter { get; set; }

        [JsonPropertyName("LastFilterModified")]
        public bool? LastFilterModified { get; set; }

        [JsonPropertyName("Filters")]
        public List<FilterModel?>? Filters { get; set; } = new();
    }

    public class FilterModel
    {
        [JsonPropertyName("Name")]
        public string? Name { get; set; }

        [JsonPropertyName("SortBy")]
        public PropertyModel? SortBy { get; set; }

        [JsonPropertyName("SortByAscending")]
        public bool? SortByAscending { get; set; }

        [JsonPropertyName("GroupBy")]
        public PropertyModel? GroupBy { get; set; }

        [JsonPropertyName("ViewType")]
        public string? ViewType { get; set; }

        [JsonPropertyName("Expression")]
        public string? Expression { get; set; }
    }

    public class PropertyModel
    {
        [JsonPropertyName("Type")]
        public string? Type { get; set; }

        [JsonPropertyName("Name")]
        public string? Name { get; set; }
    }

    public class ExternalModel
    {
        public ExternalFilterModel? LastFilter { get; set; }
        public bool LastFilterModified { get; set; }
        public List<ExternalFilterModel> Filters { get; set; } = [];

        public static ExternalModel? From(JsonModel? model)
        {
            if (model == null)
            {
                return null;
            }

            List<ExternalFilterModel> filters = [];
            if (model.Filters != null)
            {
                foreach (FilterModel? filter in model.Filters)
                {
                    var externalFilter = ExternalFilterModel.From(filter);
                    if (externalFilter != null)
                    {
                        filters.Add(externalFilter);
                    }
                }
            }

            return new ExternalModel
            {
                LastFilter = ExternalFilterModel.From(model.LastFilter),
                LastFilterModified = model.LastFilterModified ?? false,
                Filters = filters
            };
        }

        public void To(JsonModel model)
        {
            model.LastFilter = LastFilter?.To();
            model.LastFilterModified = LastFilterModified;
            model.Filters = Filters?.ConvertAll(x => x?.To()) ?? [];
        }
    }

    public class ExternalFilterModel
    {
        public string Name { get; set; } = "";
        public ExternalPropertyModel SortBy { get; set; } = new();
        public bool SortByAscending { get; set; }
        public ExternalPropertyModel? GroupBy { get; set; }
        public ViewTypeEnum ViewType { get; set; }
        public string Expression { get; set; } = "";

        public ExternalFilterModel Clone()
        {
            return From(To())!;
        }

        public FilterModel To()
        {
            return new FilterModel
            {
                Name = Name,
                SortBy = SortBy.To(),
                SortByAscending = SortByAscending,
                GroupBy = GroupBy?.To(),
                ViewType = ViewTypeToString(ViewType),
                Expression = Expression,
            };
        }

        public static ExternalFilterModel? From(FilterModel? model)
        {
            if (model == null)
            {
                return null;
            }

            return new ExternalFilterModel
            {
                Name = model.Name ?? "",
                SortBy = ExternalPropertyModel.From(model.SortBy) ?? new(),
                SortByAscending = model.SortByAscending ?? false,
                GroupBy = ExternalPropertyModel.From(model.GroupBy),
                ViewType = StringToViewType(model.ViewType ?? ""),
                Expression = model.Expression ?? "",
            };
        }

        private static string ViewTypeToString(ViewTypeEnum value)
        {
            return value switch
            {
                ViewTypeEnum.Large => VIEW_TYPE_LARGE,
                ViewTypeEnum.Medium => VIEW_TYPE_MEDIUM,
                _ => VIEW_TYPE_LARGE,
            };
        }

        private static ViewTypeEnum StringToViewType(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return ViewTypeEnum.Large;
            }
            return value switch
            {
                VIEW_TYPE_LARGE => ViewTypeEnum.Large,
                VIEW_TYPE_MEDIUM => ViewTypeEnum.Medium,
                _ => ViewTypeEnum.Large,
            };
        }
    }

    public class ExternalPropertyModel
    {
        public PropertyTypeEnum Type { get; set; }
        public string Name { get; set; } = "";

        public override bool Equals(object? obj)
        {
            if (obj is ExternalPropertyModel other)
            {
                return Type == other.Type && Name == other.Name;
            }
            return false;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Type, Name);
        }

        public PropertyModel To()
        {
            return new PropertyModel
            {
                Type = PropertyTypeToString(Type),
                Name = Name,
            };
        }

        public static ExternalPropertyModel? From(PropertyModel? model)
        {
            if (model == null)
            {
                return null;
            }

            return new ExternalPropertyModel
            {
                Type = StringToPropertyType(model.Type),
                Name = model.Name ?? "",
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
    }

    public enum PropertyTypeEnum
    {
        Title,
        Progress,
        Tag,
    }

    public enum ViewTypeEnum
    {
        Large,
        Medium,
    }
}
