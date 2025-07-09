﻿// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

#nullable disable

using System.Text.Json.Serialization;

using ComicReader.SDK.Data;

namespace ComicReader.Data.Models;

class DatabaseVersionModel : JsonDatabase<DatabaseVersionModel.JsonModel>
{
    public class JsonModel
    {
        [JsonPropertyName("database_versions_version")]
        public int DatabaseVersionsVersion { get; set; } = 0;

        [JsonPropertyName("comic_database_version")]
        public int ComicDatabaseVersion { get; set; } = 0;

        [JsonPropertyName("favorites_version")]
        public int FavoritesVersion { get; set; } = 0;

        [JsonPropertyName("history_version")]
        public int HistoryVersion { get; set; } = 0;
    }

    public static readonly DatabaseVersionModel Instance = new();

    private DatabaseVersionModel() : base("database_version.json") { }

    protected override JsonModel CreateModel()
    {
        return new();
    }

    public JsonModel GetModel()
    {
        return Read(CloneModel);
    }

    public void UpdateModel(JsonModel model)
    {
        Write(CloneModel(model));
        Save();
    }
}
