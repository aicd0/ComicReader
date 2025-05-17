// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

#nullable disable

using System.Collections.Generic;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

using ComicReader.Common;
using ComicReader.Common.Lifecycle;

namespace ComicReader.Data;

class FavoriteModel : JsonDatabase<FavoriteModel.JsonModel>
{
    public class JsonModel
    {
        [JsonPropertyName("children")]
        public List<JsonNodeModel> Children { get; set; } = new();
    }

    public class JsonNodeModel
    {
        [JsonPropertyName("type")]
        public string Type { get; set; }

        [JsonPropertyName("name")]
        public string Name { get; set; }

        [JsonPropertyName("id")]
        public long Id { get; set; }

        [JsonPropertyName("children")]
        public List<JsonNodeModel> Children { get; set; } = new();
    }

    public static readonly FavoriteModel Instance = new();

    private FavoriteModel() : base("favorites.json") { }

    protected override JsonModel CreateModel()
    {
        return new();
    }

    public async Task<ExternalModel> GetModel()
    {
        if (!await TryInitialize())
        {
            return null;
        }

        ExternalModel model = Read(ConvertToExternalModel);
        return model;
    }

    public async Task UpdateModel(ExternalModel model)
    {
        if (!await TryInitialize())
        {
            return;
        }

        Write(m =>
        {
            m.Children.Clear();
            foreach (ExternalNodeModel node in model.Children)
            {
                m.Children.Add(ConvertToJsonModel(node));
            }
            return true;
        });

        Save();
    }

    public async Task<ExternalNodeModel> FromId(long id)
    {
        if (!await TryInitialize())
        {
            return null;
        }

        JsonNodeModel model = Read(model => FromIdNoLock(model, id));
        return ConvertToExternalModel(model);
    }

    public async Task<bool> RemoveWithId(long id, bool sendEvent)
    {
        if (!await TryInitialize())
        {
            return false;
        }

        bool helper(List<JsonNodeModel> e)
        {
            for (int i = 0; i < e.Count; ++i)
            {
                JsonNodeModel node = e[i];

                if (node.Type == "i")
                {
                    if (node.Id == id)
                    {
                        e.RemoveAt(i);
                        return true;
                    }
                }
                else
                {
                    if (!(node.Children.Count == 0))
                    {
                        if (helper(node.Children))
                        {
                            return true;
                        }
                    }
                }
            }

            return false;
        }

        bool result = Write(model => helper(model.Children));
        Save();

        if (sendEvent)
        {
            DispatchUpdateEvent();
        }
        return result;
    }

    public async Task Add(long id, string title, bool sendEvent)
    {
        if (!await TryInitialize())
        {
            return;
        }

        bool updated = Write(delegate (JsonModel model)
        {
            JsonNodeModel node = FromIdNoLock(model, id);
            if (node != null)
            {
                return false;
            }
            node = new JsonNodeModel
            {
                Type = "i",
                Name = title,
                Id = id
            };
            model.Children.Add(node);
            return true;
        });

        if (updated)
        {
            Save();

            if (sendEvent)
            {
                DispatchUpdateEvent();
            }
        }
    }

    private JsonNodeModel FromIdNoLock(JsonModel model, long id)
    {
        JsonNodeModel helper(List<JsonNodeModel> e)
        {
            foreach (JsonNodeModel node in e)
            {
                if (node.Type == "i")
                {
                    if (node.Id == id)
                    {
                        return node;
                    }
                }
                else
                {
                    if (!(node.Children.Count == 0))
                    {
                        JsonNodeModel result = helper(node.Children);

                        if (result != null)
                        {
                            return result;
                        }
                    }
                }
            }

            return null;
        }

        return helper(model.Children);
    }

    private void DispatchUpdateEvent()
    {
        EventBus.Default.With(EventId.SidePaneUpdate).Emit(0);
    }

    private static ExternalModel ConvertToExternalModel(JsonModel model)
    {
        if (model == null)
        {
            return null;
        }

        var children = new List<ExternalNodeModel>();
        foreach (JsonNodeModel child in model.Children)
        {
            children.Add(ConvertToExternalModel(child));
        }
        return new ExternalModel(children);
    }

    private static ExternalNodeModel ConvertToExternalModel(JsonNodeModel node)
    {
        if (node == null)
        {
            return null;
        }

        var children = new List<ExternalNodeModel>();
        foreach (JsonNodeModel child in node.Children)
        {
            children.Add(ConvertToExternalModel(child));
        }
        return new ExternalNodeModel(node.Type, node.Name, node.Id, children);
    }

    private static JsonNodeModel ConvertToJsonModel(ExternalNodeModel node)
    {
        if (node == null)
        {
            return null;
        }
        var children = new List<JsonNodeModel>();
        foreach (ExternalNodeModel child in node.Children)
        {
            children.Add(ConvertToJsonModel(child));
        }
        return new JsonNodeModel
        {
            Type = node.Type,
            Name = node.Name,
            Id = node.Id,
            Children = children
        };
    }

    public class ExternalModel(List<ExternalNodeModel> nodes)
    {
        public List<ExternalNodeModel> Children { get; set; } = nodes;
    }

    public class ExternalNodeModel(string type, string name, long id, List<ExternalNodeModel> nodes)
    {
        public string Type { get; set; } = type;
        public string Name { get; set; } = name;
        public long Id { get; set; } = id;
        public List<ExternalNodeModel> Children { get; set; } = nodes;
    }
}
