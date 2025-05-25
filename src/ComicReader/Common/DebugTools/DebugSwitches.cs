// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

using ComicReader.SDK.Common.DebugTools;
using ComicReader.SDK.Common.KVStorage;

namespace ComicReader.Common.DebugTools;

internal class DebugSwitches
{
    private const string KEY_DEBUG_MODE = "debug_mode";

    public static readonly DebugSwitches Instance = new();

    private CommonConfig? _config;
    private LogTag? _consoleWhitelist;

    private readonly JsonSerializerOptions _serializeOption = new()
    {
        WriteIndented = true,
    };

    private DebugSwitches() { }

    private static bool? _debugMode = null;
    public static bool DebugMode
    {
        get
        {
            if (!_debugMode.HasValue)
            {
                _debugMode = KVDatabase.GetDefaultMethod().GetBoolean(GlobalConstants.KV_DB_APP, KEY_DEBUG_MODE, DebugUtils.DebugBuild);
            }
            return _debugMode.Value;
        }
        set
        {
            _debugMode = value;
            KVDatabase.GetDefaultMethod().SetBoolean(GlobalConstants.KV_DB_APP, KEY_DEBUG_MODE, value);
            DebugUtils.DebugMode = value;
        }
    }

    private bool ConsoleEnabled
    {
        get => DebugUtils.DebugBuild && GetConfig().ConsoleEnabled;
        set
        {
            GetConfig().ConsoleEnabled = value;
            SaveConfig();
        }
    }

    private bool LogTreeEnabled
    {
        get => DebugUtils.DebugBuild && GetConfig().LogTreeEnabled;
        set
        {
            GetConfig().LogTreeEnabled = value;
            SaveConfig();
        }
    }

    private LogTag? ConsoleWhitelist
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

    public void Initialize()
    {
        DebugUtils.DebugMode = DebugMode;
        ApplyCommonConfigs();
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

    private void InvalidateCache()
    {
        _consoleWhitelist = null;
    }

    private void SaveConfig()
    {
        if (_config == null)
        {
            return;
        }
        string json = JsonSerializer.Serialize(_config);
        KVDatabase.GetDefaultMethod().SetString(GlobalConstants.KV_DB_DEV_TOOLS, "debug_switches", json);
        ApplyCommonConfigs();
    }

    private void ApplyCommonConfigs()
    {
        Logger.SetConsoleEnabled(ConsoleEnabled);
        Logger.SetConsoleWhitelist(ConsoleWhitelist);
        Logger.SetLogTreeEnabled(LogTreeEnabled);
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
