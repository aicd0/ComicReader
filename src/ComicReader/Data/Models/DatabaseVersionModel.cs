// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

#nullable disable

using System.Text.Json.Serialization;
using System.Threading.Tasks;

using ComicReader.SDK.Data;

namespace ComicReader.Data.Models;

class DatabaseVersionModel : JsonDatabase<DatabaseVersionModel.JsonModel>
{
    public class JsonModel
    {
        [JsonPropertyName("favorites_version")]
        public int FavoritesVersion { get; set; } = 0;
    }

    public static readonly DatabaseVersionModel Instance = new();

    private DatabaseVersionModel() : base("database_version.json") { }

    protected override JsonModel CreateModel()
    {
        return new();
    }

    public async Task<JsonModel> GetModel()
    {
        return await Read(CloneModel);
    }

    public async Task UpdateModel(JsonModel model)
    {
        await Write(CloneModel(model));
        await Save();
    }
}
