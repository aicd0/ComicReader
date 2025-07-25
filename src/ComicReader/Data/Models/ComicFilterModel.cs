﻿// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

using ComicReader.SDK.Data;

namespace ComicReader.Data.Models;

class ComicFilterModel : JsonDatabase<ComicFilterModel.JsonModel>
{
    public const string VIEW_TYPE_LARGE = "Large";
    public const string VIEW_TYPE_MEDIUM = "Medium";
    public const string FUNCTION_TYPE_NONE = "None";
    public const string FUNCTION_TYPE_ITEM_COUNT = "ItemCount";
    public const string FUNCTION_TYPE_MAX = "Max";
    public const string FUNCTION_TYPE_MIN = "Min";
    public const string FUNCTION_TYPE_SUM = "Sum";
    public const string FUNCTION_TYPE_AVERAGE = "Average";

    public static readonly ComicFilterModel Instance = new();

    private ComicFilterModel() : base("filters.json") { }

    protected override JsonModel CreateModel()
    {
        return new();
    }

    public ExternalModel? GetModel()
    {
        return Read(ExternalModel.From);
    }

    public void UpdateModel(ExternalModel model)
    {
        Write(m =>
        {
            model.To(m);
            return true;
        });
        Save();
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
        public JsonNode? SortBy { get; set; }

        [JsonPropertyName("SortByAscending")]
        public bool? SortByAscending { get; set; }

        [JsonPropertyName("GroupBy")]
        public JsonNode? GroupBy { get; set; }

        [JsonPropertyName("GroupByAscending")]
        public bool? GroupByAscending { get; set; }

        [JsonPropertyName("GroupSortingFunction")]
        public string? GroupSortingFunction { get; set; }

        [JsonPropertyName("GroupSortingProperty")]
        public JsonNode? GroupSortingProperty { get; set; }

        [JsonPropertyName("ViewType")]
        public string? ViewType { get; set; }

        [JsonPropertyName("Expression")]
        public string? Expression { get; set; }
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
        public ComicPropertyModel SortBy { get; set; } = new();
        public bool SortByAscending { get; set; }
        public ComicPropertyModel? GroupBy { get; set; }
        public bool GroupByAscending { get; set; }
        public FunctionTypeEnum GroupSortingFunction { get; set; } = FunctionTypeEnum.None;
        public ComicPropertyModel? GroupSortingProperty { get; set; }
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
                SortBy = SortBy.ToJson(),
                SortByAscending = SortByAscending,
                GroupBy = GroupBy?.ToJson(),
                GroupByAscending = GroupByAscending,
                GroupSortingFunction = FunctionTypeToString(GroupSortingFunction),
                GroupSortingProperty = GroupSortingProperty?.ToJson(),
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
                SortBy = ComicPropertyModel.FromJson(model.SortBy) ?? new(),
                SortByAscending = model.SortByAscending ?? false,
                GroupBy = ComicPropertyModel.FromJson(model.GroupBy),
                GroupByAscending = model.GroupByAscending ?? false,
                GroupSortingFunction = StringToFunctionType(model.GroupSortingFunction ?? FUNCTION_TYPE_NONE),
                GroupSortingProperty = ComicPropertyModel.FromJson(model.GroupSortingProperty),
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

        public static string FunctionTypeToString(FunctionTypeEnum value)
        {
            return value switch
            {
                FunctionTypeEnum.None => FUNCTION_TYPE_NONE,
                FunctionTypeEnum.ItemCount => FUNCTION_TYPE_ITEM_COUNT,
                FunctionTypeEnum.Max => FUNCTION_TYPE_MAX,
                FunctionTypeEnum.Min => FUNCTION_TYPE_MIN,
                FunctionTypeEnum.Sum => FUNCTION_TYPE_SUM,
                FunctionTypeEnum.Average => FUNCTION_TYPE_AVERAGE,
                _ => FUNCTION_TYPE_NONE,
            };
        }

        public static FunctionTypeEnum StringToFunctionType(string value)
        {
            return value switch
            {
                FUNCTION_TYPE_NONE => FunctionTypeEnum.None,
                FUNCTION_TYPE_ITEM_COUNT => FunctionTypeEnum.ItemCount,
                FUNCTION_TYPE_MAX => FunctionTypeEnum.Max,
                FUNCTION_TYPE_MIN => FunctionTypeEnum.Min,
                FUNCTION_TYPE_SUM => FunctionTypeEnum.Sum,
                FUNCTION_TYPE_AVERAGE => FunctionTypeEnum.Average,
                _ => FunctionTypeEnum.None,
            };
        }
    }

    public enum ViewTypeEnum
    {
        Large,
        Medium,
    }

    public enum FunctionTypeEnum
    {
        None,
        ItemCount,
        Max,
        Min,
        Sum,
        Average,
    }
}
