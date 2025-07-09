// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;

using ComicReader.Data.Legacy;
using ComicReader.Data.Models;

namespace ComicReader.Data;

class DatabaseUpgradeManager
{
    public static DatabaseUpgradeManager Instance = new();

    private DatabaseUpgradeManager() { }

    public void UpgradeDatabase()
    {
        DatabaseVersionModel.JsonModel databaseVersions = DatabaseVersionModel.Instance.GetModel();
        if (databaseVersions == null)
        {
            return;
        }
        List<Func<DatabaseVersionModel.JsonModel, bool>> tasks = [
            UpgradeDatabaseVersions,
            UpgradeComicDatabase,
            UpgradeFavorites,
            UpgradeHistory,
            UpgradeAppSettings,
        ];
        foreach (Func<DatabaseVersionModel.JsonModel, bool> task in tasks)
        {
            if (task(databaseVersions))
            {
                DatabaseVersionModel.Instance.UpdateModel(databaseVersions);
            }
        }
    }

    private bool UpgradeDatabaseVersions(DatabaseVersionModel.JsonModel versions)
    {
        if (versions.DatabaseVersionsVersion >= 1)
        {
            return false;
        }

        versions.ComicDatabaseVersion = XmlDatabase.Settings.DatabaseVersion;
        versions.DatabaseVersionsVersion = 1;
        return true;
    }

    private bool UpgradeComicDatabase(DatabaseVersionModel.JsonModel versions)
    {
        if (versions.ComicDatabaseVersion >= SqlDatabaseManager.DATABASE_VERSION)
        {
            return false;
        }

        SqlDatabaseManager.UpdateDatabase(versions.ComicDatabaseVersion);
        versions.ComicDatabaseVersion = SqlDatabaseManager.DATABASE_VERSION;
        return true;
    }

    private bool UpgradeFavorites(DatabaseVersionModel.JsonModel versions)
    {
        if (versions.FavoritesVersion >= 1)
        {
            return false;
        }

        FavoriteData oldData = XmlDatabase.Favorites;
        if (oldData != null)
        {
            FavoriteModel.ExternalModel newData = new([]);
            FavoriteModel.ExternalNodeModel CloneNode(FavoriteNodeData node)
            {
                FavoriteModel.ExternalNodeModel newNode = new(node.Type, node.Name, node.Id, []);
                if (node.Children != null)
                {
                    foreach (FavoriteNodeData child in node.Children)
                    {
                        newNode.Children.Add(CloneNode(child));
                    }
                }
                return newNode;
            }
            foreach (FavoriteNodeData child in oldData.RootNodes)
            {
                newData.Children.Add(CloneNode(child));
            }
            FavoriteModel.Instance.UpdateModel(newData);
        }
        versions.FavoritesVersion = 1;
        return true;
    }

    private bool UpgradeHistory(DatabaseVersionModel.JsonModel versions)
    {
        if (versions.HistoryVersion >= 1)
        {
            return false;
        }

        HistoryData oldData = XmlDatabase.History;
        if (oldData != null)
        {
            HistoryModel.ExternalModel newData = new([]);
            foreach (HistoryItemData item in oldData.Items)
            {
                newData.Items.Add(new HistoryModel.ExternalItemModel(item.Id, item.Title, item.DateTime));
            }
            HistoryModel.Instance.UpdateModel(newData);
        }

        versions.HistoryVersion = 1;
        return true;
    }

    private bool UpgradeAppSettings(DatabaseVersionModel.JsonModel versions)
    {
        if (versions.AppSettingVersion >= 1)
        {
            return false;
        }

        SettingData oldModel = XmlDatabase.Settings;
        if (oldModel != null)
        {
            AppSettingsModel.ExternalModel newModel = AppSettingsModel.Instance.GetModel();
            newModel.ComicFolders = oldModel.ComicFolders ?? [];
            AppSettingsModel.ReaderSettingModel readerSettings = newModel.DefaultReaderSetting;
            readerSettings.VerticalReading = oldModel.VerticalReading;
            readerSettings.LeftToRight = oldModel.LeftToRight;
            readerSettings.VerticalContinuous = oldModel.VerticalContinuous;
            readerSettings.HorizontalContinuous = oldModel.HorizontalContinuous;
            readerSettings.VerticalPageArrangement = oldModel.VerticalPageArrangement;
            readerSettings.HorizontalPageArrangement = oldModel.HorizontalPageArrangement;
            AppSettingsModel.Instance.UpdateModel(newModel);
        }

        versions.AppSettingVersion = 1;
        return true;
    }
}
