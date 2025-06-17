// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

using ComicReader.SDK.Data;

namespace ComicReader.Data.Models;

class AppSettingsModel : JsonDatabase<AppSettingsModel.JsonModel>
{
    public static readonly AppSettingsModel Instance = new();

    private AppSettingsModel() : base("settings.json") { }

    protected override JsonModel CreateModel()
    {
        return new();
    }

    public async Task<ExternalModel> GetModel()
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
        [JsonPropertyName("RemoveUnreachableComics")]
        public bool? RemoveUnreachableComics { get; set; }

        [JsonPropertyName("Language")]
        public string? Language { get; set; }

        [JsonPropertyName("Theme")]
        public int? Theme { get; set; }
    }

    public class ExternalModel
    {
        public bool RemoveUnreachableComics { get; set; }
        public string Language { get; set; } = "";
        public AppearanceSetting Theme { get; set; } = AppearanceSetting.UseSystemSetting;

        public static ExternalModel From(JsonModel model)
        {
            ExternalModel externalModel = new()
            {
                RemoveUnreachableComics = model.RemoveUnreachableComics ?? true,
                Language = model.Language ?? ""
            };

            int? theme = model.Theme;
            if (theme is null)
            {
                externalModel.Theme = AppearanceSetting.UseSystemSetting;
            }
            else if (Enum.IsDefined(typeof(AppearanceSetting), theme))
            {
                externalModel.Theme = (AppearanceSetting)theme;
            }
            else
            {
                externalModel.Theme = AppearanceSetting.UseSystemSetting;
            }

            return externalModel;
        }

        public void To(JsonModel model)
        {
            model.RemoveUnreachableComics = RemoveUnreachableComics;
            model.Language = Language;
            model.Theme = (int)Theme;
        }
    }

    public enum AppearanceSetting
    {
        Light,
        Dark,
        UseSystemSetting,
        None
    }
}
