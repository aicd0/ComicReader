// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

#nullable disable

using System.Text.Json;
using System.Text.Json.Serialization;

using ComicReader.Common.KVStorage;

namespace ComicReader.Common.DebugTools;

internal class DebugSwitches
{
    public static readonly DebugSwitches Instance = new();

    private readonly JsonSerializerOptions _serializeOption = new()
    {
        WriteIndented = true,
    };

    private DebugSwitches() { }

    readonly CachedBoolean _consoleEnabled = new("console_enabled", true);
    public bool ConsoleEnabled
    {
        get => DebugUtils.DebugBuild && _consoleEnabled.Get();
        set
        {
            _consoleEnabled.Set(value);
        }
    }

    readonly CachedBoolean _logTreeEnabled = new("log_tree_enabled", true);
    public bool LogTreeEnabled
    {
        get => DebugUtils.DebugBuild && _logTreeEnabled.Get();
        set
        {
            _logTreeEnabled.Set(value);
        }
    }

    public string SerializeToJson()
    {
        var config = new CommonConfig
        {
            ConsoleEnabled = _consoleEnabled.Get(),
            LogTreeEnabled = _logTreeEnabled.Get()
        };

        return JsonSerializer.Serialize(config, _serializeOption);
    }

    public void ParseFromJson(string json)
    {
        CommonConfig config = JsonSerializer.Deserialize<CommonConfig>(json);

        ConsoleEnabled = config.ConsoleEnabled;
        LogTreeEnabled = config.LogTreeEnabled;
    }

    //
    // Classes
    //

    public class CommonConfig
    {
        [JsonPropertyName("console_enabled")]
        public bool ConsoleEnabled { get; set; }

        [JsonPropertyName("log_tree_enabled")]
        public bool LogTreeEnabled { get; set; }
    }

    private class CachedBoolean(string key, bool defaultValue)
    {
        private bool? _value;

        public bool Get()
        {
            if (!_value.HasValue)
            {
                _value = KVDatabase.GetDefaultMethod().GetBoolean(GlobalConstants.KV_DB_DEV_TOOLS, key, defaultValue);
            }

            return _value.Value;
        }

        public void Set(bool value)
        {
            _value = value;
            KVDatabase.GetDefaultMethod().SetBoolean(GlobalConstants.KV_DB_DEV_TOOLS, key, value);
        }
    }
}
