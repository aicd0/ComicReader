// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

using ComicReader.Common;
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

    public ExternalModel GetModel()
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

    public void AddComicFolder(string folderPath)
    {
        bool updated = Write(m =>
        {
            m.ComicFolders ??= [];
            for (int i = m.ComicFolders.Count - 1; i >= 0; i--)
            {
                string? oldPath = m.ComicFolders[i];
                if (string.IsNullOrEmpty(oldPath))
                {
                    m.ComicFolders.RemoveAt(i);
                    continue;
                }
                if (StringUtils.FolderContain(oldPath, folderPath))
                {
                    return false;
                }
                if (StringUtils.FolderContain(folderPath, oldPath))
                {
                    m.ComicFolders.RemoveAt(i);
                }
            }
            m.ComicFolders.Add(folderPath);
            return true;
        });
        if (updated)
        {
            Save();
        }
    }

    public void RemoveComicFolder(string folderPath)
    {
        bool updated = Write(m =>
        {
            m.ComicFolders ??= [];
            return m.ComicFolders.Remove(folderPath);
        });
        if (updated)
        {
            Save();
        }
    }

    public class JsonModel
    {
        [JsonPropertyName("ComicFolders")]
        public List<string?>? ComicFolders { get; set; }

        [JsonPropertyName("RemoveUnreachableComics")]
        public bool? RemoveUnreachableComics { get; set; }

        [JsonPropertyName("Language")]
        public string? Language { get; set; }

        [JsonPropertyName("Theme")]
        public int? Theme { get; set; }
    }

    public class ExternalModel
    {
        public List<string> ComicFolders { get; set; } = [];
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

            if (model.ComicFolders is not null)
            {
                foreach (string? folder in model.ComicFolders)
                {
                    if (string.IsNullOrEmpty(folder))
                    {
                        continue;
                    }
                    externalModel.ComicFolders.Add(folder);
                }
            }

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
            model.ComicFolders = [.. ComicFolders];
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
