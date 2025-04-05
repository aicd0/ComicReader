// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace ComicReader.Data;

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
        if (!await TryInitialize())
        {
            return null;
        }

        return Read(CloneModel);
    }

    public async Task UpdateModel(JsonModel model)
    {
        if (!await TryInitialize())
        {
            return;
        }

        CloneFrom(model);
        Save();
    }
}
