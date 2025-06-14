// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

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
    }

    public class ExternalModel
    {
        public bool RemoveUnreachableComics { get; set; }

        public static ExternalModel From(JsonModel model)
        {
            return new ExternalModel
            {
                RemoveUnreachableComics = model.RemoveUnreachableComics ?? true
            };
        }

        public void To(JsonModel model)
        {
            model.RemoveUnreachableComics = RemoveUnreachableComics;
        }
    }
}
