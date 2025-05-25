// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

using ComicReader.SDK.Common.KVStorage;

namespace ComicReader.Common.DebugTools;

internal class DebugSwitches
{
    public static readonly DebugSwitches Instance = new();

    private CommonConfig? _config;
    private LogTag? _consoleWhitelist;

    private readonly JsonSerializerOptions _serializeOption = new()
    {
        WriteIndented = true,
    };

    private DebugSwitches() { }

    public bool ConsoleEnabled
    {
        get => DebugUtils.DebugBuild && GetConfig().ConsoleEnabled;
        set
        {
            GetConfig().ConsoleEnabled = value;
            SaveConfig();
        }
    }

    public bool LogTreeEnabled
    {
        get => DebugUtils.DebugBuild && GetConfig().LogTreeEnabled;
        set
        {
            GetConfig().LogTreeEnabled = value;
            SaveConfig();
        }
    }

    public LogTag? ConsoleWhitelist
    {
        get
        {
            LogTag? tag = _consoleWhitelist;
            if (tag != null)
            {
                return tag;
            }
            JsonObject? jsonObject = GetConfig().ConsoleWhitelist;
            if (jsonObject == null)
            {
                return null;
            }
            tag = LogTag.FromJson(jsonObject);
            _consoleWhitelist = tag;
            return tag;
        }
    }

    public string SerializeToJson()
    {
        return JsonSerializer.Serialize(GetConfig(), _serializeOption);
    }

    public void SaveConfig(string json)
    {
        _config = JsonSerializer.Deserialize<CommonConfig>(json) ?? new();
        InvalidateCache();
        SaveConfig();
    }

    private void InvalidateCache()
    {
        _consoleWhitelist = null;
    }

    private CommonConfig GetConfig()
    {
        CommonConfig? config = _config;
        if (config != null)
        {
            return config;
        }
        config = ReadConfig() ?? new();
        _config = config;
        InvalidateCache();
        return config;
    }

    private CommonConfig ReadConfig()
    {
        string json = KVDatabase.GetDefaultMethod().GetString(GlobalConstants.KV_DB_DEV_TOOLS, "debug_switches", string.Empty);
        CommonConfig? config = null;
        if (!string.IsNullOrEmpty(json))
        {
            try
            {
                config = JsonSerializer.Deserialize<CommonConfig>(json);
            }
            catch (JsonException ex)
            {
                Logger.F("DebugSwitches", nameof(_config), ex);
            }
        }
        return config ?? new();
    }

    private void SaveConfig()
    {
        if (_config == null)
        {
            return;
        }
        string json = JsonSerializer.Serialize(_config);
        KVDatabase.GetDefaultMethod().SetString(GlobalConstants.KV_DB_DEV_TOOLS, "debug_switches", json);
    }

    //
    // Classes
    //

    public class CommonConfig
    {
        [JsonPropertyName("console_enabled")]
        public bool ConsoleEnabled { get; set; }

        [JsonPropertyName("console_whitelist")]
        public JsonObject? ConsoleWhitelist { get; set; }

        [JsonPropertyName("log_tree_enabled")]
        public bool LogTreeEnabled { get; set; }
    }
}
