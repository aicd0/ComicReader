﻿// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text;

using ComicReader.Common.Lifecycle;
using ComicReader.Data.Models.Comic;
using ComicReader.SDK.Common.Threading;

namespace ComicReader.Views.Reader;

internal partial class EditComicInfoDialogViewModel : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    private readonly List<ComicModel> _comics = [];
    private string _title1 = string.Empty;
    private string _title2 = string.Empty;
    private string _description = string.Empty;
    private string _tags = string.Empty;
    private bool _tagDiffMode = true;
    private bool _tagIdMode = false;
    private bool _title1Changed = false;
    private bool _title2Changed = false;
    private bool _descriptionChanged = false;
    private bool _tagsChanged = false;
    private Dictionary<TagWithId, HashSet<TagWithId>> _commonTags = [];

    public MutableLiveData<string> Title1TextLiveData = new();
    public MutableLiveData<string> Title2TextLiveData = new();
    public MutableLiveData<string> DescriptionTextLiveData = new();
    public MutableLiveData<string> TagTextLiveData = new();
    public MutableLiveData<bool> Title1ChangedLiveData = new();
    public MutableLiveData<bool> Title2ChangedLiveData = new();
    public MutableLiveData<bool> DescriptionChangedLiveData = new();
    public MutableLiveData<bool> TagChangedLiveData = new();

    private bool _isTagInfoBarOpen = false;
    public bool IsTagInfoBarOpen
    {
        get => _isTagInfoBarOpen;
        set
        {
            _isTagInfoBarOpen = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsTagInfoBarOpen)));
        }
    }

    public void Initialize(IEnumerable<ComicModel> comics)
    {
        _comics.AddRange(comics);

        Title1ChangedLiveData.Emit(false);
        Title2ChangedLiveData.Emit(false);
        DescriptionChangedLiveData.Emit(false);

        _title1 = ToStandardString(ExtractCommonValue((comic) => comic.Title1, string.Empty));
        Title1TextLiveData.Emit(_title1);

        _title2 = ToStandardString(ExtractCommonValue((comic) => comic.Title2, string.Empty));
        Title2TextLiveData.Emit(_title2);

        _description = ToStandardString(ExtractCommonValue((comic) => comic.Description, string.Empty));
        DescriptionTextLiveData.Emit(_description);

        InitializeTags(_tagIdMode);
    }

    public void Save()
    {
        List<KeyValuePair<TagWithId, List<TagWithId>>> newTags = ParseTagString(_tags, _tagIdMode);

        TaskDispatcher.DefaultQueue.Submit("ContentDialogPrimaryButtonClick", delegate
        {
            foreach (ComicModel comic in _comics)
            {
                if (_title1Changed)
                {
                    comic.SetTitle1(_title1);
                }
                if (_title2Changed)
                {
                    comic.SetTitle2(_title2);
                }
                if (_descriptionChanged)
                {
                    comic.SetDescription(_description);
                }
                if (_tagsChanged)
                {
                    Dictionary<string, HashSet<string>> comicTags = [];
                    foreach (ComicData.TagData tagData in comic.Tags)
                    {
                        comicTags[tagData.Name] = [.. tagData.Tags];
                    }
                    comic.SetTags(MergeTags(comicTags, _commonTags, newTags, _tagDiffMode, _tagIdMode));
                }
            }
        });
    }

    public void SetTitle1(string text)
    {
        text = ToStandardString(text);
        if (text == _title1)
        {
            return;
        }
        _title1 = text;
        _title1Changed = true;
        Title1ChangedLiveData.Emit(true);
    }

    public void SetTitle2(string text)
    {
        text = ToStandardString(text);
        if (text == _title2)
        {
            return;
        }
        _title2 = text;
        _title2Changed = true;
        Title2ChangedLiveData.Emit(true);
    }

    public void SetDescription(string text)
    {
        text = ToStandardString(text);
        if (text == _description)
        {
            return;
        }
        _description = text;
        _descriptionChanged = true;
        DescriptionChangedLiveData.Emit(true);
    }

    public void SetTags(string text)
    {
        text = ToStandardString(text);
        if (text == _tags)
        {
            return;
        }
        _tags = text;
        MarkTagChange(true);
    }

    public void SetTagDiffMode(bool diffMode)
    {
        _tagDiffMode = diffMode;
        MarkTagChange(true);
    }

    public void SetTagIdMode(bool tagIdMode)
    {
        if (_tagIdMode == tagIdMode)
        {
            return;
        }
        _tagIdMode = tagIdMode;

        InitializeTags(tagIdMode);
    }

    private T ExtractCommonValue<T>(Func<ComicModel, T> extractor, T defaultValue)
    {
        T lastValue = defaultValue;
        for (int i = 0; i < _comics.Count; i++)
        {
            ComicModel comic = _comics[i];
            if (i == 0)
            {
                lastValue = extractor(comic);
            }
            else if (!EqualityComparer<T>.Default.Equals(lastValue, extractor(comic)))
            {
                lastValue = defaultValue;
                break;
            }
        }
        return lastValue;
    }

    //
    // Tags
    //

    private void InitializeTags(bool tagIdMode)
    {
        MarkTagChange(false);
        Dictionary<TagWithId, HashSet<TagWithId>> commonTags = [];
        for (int i = 0; i < _comics.Count; i++)
        {
            ComicModel comic = _comics[i];
            Dictionary<TagWithId, HashSet<TagWithId>> comicTags = [];
            foreach (ComicData.TagData tagData in comic.Tags)
            {
                HashSet<TagWithId> tags = [];
                foreach (string tag in tagData.Tags)
                {
                    tags.Add(new(tag));
                }
                comicTags[new(tagData.Name)] = tags;
            }
            if (tagIdMode)
            {
                foreach (KeyValuePair<TagWithId, HashSet<TagWithId>> pair in comicTags)
                {
                    if (commonTags.TryGetValue(pair.Key, out HashSet<TagWithId>? tags))
                    {
                        tags.UnionWith(pair.Value);
                    }
                    else
                    {
                        commonTags[pair.Key] = pair.Value;
                    }
                }
            }
            else
            {
                if (i == 0)
                {
                    foreach (KeyValuePair<TagWithId, HashSet<TagWithId>> pair in comicTags)
                    {
                        commonTags[pair.Key] = pair.Value;
                    }
                }
                else
                {
                    List<TagWithId> keys = [.. commonTags.Keys];
                    foreach (TagWithId key in keys)
                    {
                        if (comicTags.TryGetValue(key, out HashSet<TagWithId>? tags))
                        {
                            commonTags[key].IntersectWith(tags);
                        }
                        else
                        {
                            commonTags.Remove(key);
                        }
                    }
                }
            }
        }
        {
            int nextId = 0;
            foreach (KeyValuePair<TagWithId, HashSet<TagWithId>> pair in commonTags)
            {
                pair.Key.Id = nextId++;
                foreach (TagWithId tag in pair.Value)
                {
                    tag.Id = nextId++;
                }
            }
        }
        _commonTags = commonTags;

        StringBuilder sb = new();
        foreach (KeyValuePair<TagWithId, HashSet<TagWithId>> pair in commonTags)
        {
            if (tagIdMode)
            {
                sb.Append(pair.Key.Id).Append('#').Append(pair.Key.Content);
            }
            else
            {
                sb.Append(pair.Key.Content);
            }
            sb.Append(": ");
            bool first = true;
            foreach (TagWithId tag in pair.Value)
            {
                if (!first)
                {
                    sb.Append('/');
                }
                first = false;
                if (tagIdMode)
                {
                    sb.Append(tag.Id).Append('#').Append(tag.Content);
                }
                else
                {
                    sb.Append(tag.Content);
                }
            }
            sb.Append('\n');
        }
        _tags = ToStandardString(sb.ToString());
        TagTextLiveData.Emit(_tags);
    }

    private void MarkTagChange(bool changed)
    {
        _tagsChanged = changed;
        TagChangedLiveData.Emit(changed);
    }

    private static Dictionary<string, HashSet<string>> MergeTags(Dictionary<string, HashSet<string>> comicTags,
        Dictionary<TagWithId, HashSet<TagWithId>> oldTags, List<KeyValuePair<TagWithId, List<TagWithId>>> newTags, bool diffMode, bool tagIdMode)
    {
        if (!diffMode)
        {
            Dictionary<string, HashSet<string>> result = [];
            foreach (KeyValuePair<TagWithId, List<TagWithId>> pair in newTags)
            {
                if (!result.TryGetValue(pair.Key.Content, out HashSet<string>? tags))
                {
                    tags = [];
                    result[pair.Key.Content] = tags;
                }
                foreach (TagWithId tag in pair.Value)
                {
                    tags.Add(tag.Content);
                }
            }
            return result;
        }

        if (!tagIdMode)
        {
            HashSet<TagWithId> oldKeys = [];
            foreach (TagWithId tag in oldTags.Keys)
            {
                oldKeys.Add(tag);
            }
            HashSet<TagWithId> newKeys = [];
            foreach (KeyValuePair<TagWithId, List<TagWithId>> pair in newTags)
            {
                newKeys.Add(pair.Key);
            }
            {
                HashSet<TagWithId> removedKeys = [.. oldKeys];
                removedKeys.ExceptWith(newKeys);
                foreach (TagWithId key in removedKeys)
                {
                    comicTags.Remove(key.Content);
                }
            }
            {
                HashSet<TagWithId> addedKeys = [.. newKeys];
                addedKeys.ExceptWith(oldKeys);
                foreach (TagWithId key in addedKeys)
                {
                    if (!comicTags.TryGetValue(key.Content, out HashSet<string>? tags))
                    {
                        tags = [];
                        comicTags[key.Content] = tags;
                    }
                    foreach (KeyValuePair<TagWithId, List<TagWithId>> pair in newTags)
                    {
                        if (pair.Key.Content == key.Content)
                        {
                            foreach (TagWithId tag in pair.Value)
                            {
                                tags.Add(tag.Content);
                            }
                        }
                    }
                }
            }
            {
                HashSet<TagWithId> existingKeys = [.. oldKeys];
                existingKeys.IntersectWith(newKeys);
                foreach (TagWithId key in existingKeys)
                {
                    if (!comicTags.TryGetValue(key.Content, out HashSet<string>? tags))
                    {
                        continue;
                    }
                    HashSet<TagWithId> oldValues = [.. oldTags[key]];
                    HashSet<TagWithId> newValues = [];
                    foreach (KeyValuePair<TagWithId, List<TagWithId>> pair in newTags)
                    {
                        if (pair.Key.Content == key.Content)
                        {
                            foreach (TagWithId tag in pair.Value)
                            {
                                newValues.Add(tag);
                            }
                        }
                    }
                    {
                        HashSet<TagWithId> removedValues = [.. oldValues];
                        removedValues.ExceptWith(newValues);
                        foreach (TagWithId tag in removedValues)
                        {
                            tags.Remove(tag.Content);
                        }
                    }
                    {
                        HashSet<TagWithId> addedValues = [.. newValues];
                        addedValues.ExceptWith(oldValues);
                        foreach (TagWithId tag in addedValues)
                        {
                            tags.Add(tag.Content);
                        }
                    }
                }
            }
            return comicTags;
        }

        {
            Dictionary<int, KeyValuePair<TagWithId, HashSet<TagWithId>>> oldCategoryIds = [];
            foreach (KeyValuePair<TagWithId, HashSet<TagWithId>> pair in oldTags)
            {
                oldCategoryIds[pair.Key.Id] = pair;
            }
            Dictionary<int, KeyValuePair<TagWithId, List<TagWithId>>> newCategoryIds = [];
            foreach (KeyValuePair<TagWithId, List<TagWithId>> pair in newTags)
            {
                newCategoryIds[pair.Key.Id] = pair;
            }
            {
                Dictionary<int, KeyValuePair<TagWithId, HashSet<TagWithId>>> removedCategoryIds = new(oldCategoryIds);
                foreach (int id in newCategoryIds.Keys)
                {
                    removedCategoryIds.Remove(id);
                }
                foreach (KeyValuePair<TagWithId, HashSet<TagWithId>> pair in removedCategoryIds.Values)
                {
                    comicTags.Remove(pair.Key.Content);
                }
            }
            {
                HashSet<int> updatedCategoryIds = [.. oldCategoryIds.Keys];
                {
                    HashSet<int> newIds = [.. newCategoryIds.Keys];
                    updatedCategoryIds.IntersectWith(newIds);
                }
                foreach (int updatedCategoryId in updatedCategoryIds)
                {
                    KeyValuePair<TagWithId, HashSet<TagWithId>> oldCategory = oldCategoryIds[updatedCategoryId];
                    KeyValuePair<TagWithId, List<TagWithId>> newCategory = newCategoryIds[updatedCategoryId];
                    if (!comicTags.Remove(oldCategory.Key.Content, out HashSet<string>? tags))
                    {
                        continue;
                    }
                    comicTags[newCategory.Key.Content] = tags;
                    Dictionary<int, TagWithId> oldIds = [];
                    foreach (TagWithId tag in oldCategory.Value)
                    {
                        oldIds[tag.Id] = tag;
                    }
                    Dictionary<int, TagWithId> newIds = [];
                    foreach (TagWithId tag in newCategory.Value)
                    {
                        newIds[tag.Id] = tag;
                    }
                    {
                        HashSet<int> removedIds = [.. oldIds.Keys];
                        foreach (int id in newIds.Keys)
                        {
                            removedIds.Remove(id);
                        }
                        foreach (int id in removedIds)
                        {
                            tags.Remove(oldIds[id].Content);
                        }
                    }
                    {
                        HashSet<int> updatedIds = [.. oldIds.Keys];
                        {
                            HashSet<int> newIdSet = [.. newIds.Keys];
                            updatedIds.IntersectWith(newIdSet);
                        }
                        foreach (int id in updatedIds)
                        {
                            if (tags.Remove(oldIds[id].Content))
                            {
                                tags.Add(newIds[id].Content);
                            }
                        }
                    }
                    {
                        HashSet<int> addedIds = [.. newIds.Keys];
                        foreach (int id in oldIds.Keys)
                        {
                            addedIds.Remove(id);
                        }
                        foreach (int id in addedIds)
                        {
                            tags.Add(newIds[id].Content);
                        }
                    }
                }
            }
            {
                Dictionary<int, KeyValuePair<TagWithId, List<TagWithId>>> addedCategoryIds = new(newCategoryIds);
                foreach (int id in oldCategoryIds.Keys)
                {
                    addedCategoryIds.Remove(id);
                }
                foreach (KeyValuePair<TagWithId, List<TagWithId>> pair in addedCategoryIds.Values)
                {
                    if (!comicTags.TryGetValue(pair.Key.Content, out HashSet<string>? tags))
                    {
                        tags = [];
                        comicTags[pair.Key.Content] = tags;
                    }
                    foreach (TagWithId tag in pair.Value)
                    {
                        tags.Add(tag.Content);
                    }
                }
            }
            return comicTags;
        }
    }

    private static List<KeyValuePair<TagWithId, List<TagWithId>>> ParseTagString(string text, bool tagIdMode)
    {
        List<KeyValuePair<TagWithId, List<TagWithId>>> result = [];
        string[] properties = text.Split("\n", StringSplitOptions.RemoveEmptyEntries);
        foreach (string property in properties)
        {
            ParsePropertyResult? parseResult = ParseProperty(property, tagIdMode);
            if (parseResult == null)
            {
                continue;
            }
            result.Add(new(parseResult.Name, parseResult.Tags));
        }
        return result;
    }

    private static ParsePropertyResult? ParseProperty(string src, bool tagIdMode)
    {
        string[] pieces = src.Split(":", 2);
        if (pieces.Length != 2)
        {
            return null;
        }
        TagWithId? name = ParseTag(pieces[0], tagIdMode);
        if (name is null)
        {
            return null;
        }
        var result = new ParsePropertyResult(name);
        var tags = new List<string>(pieces[1].Split("/", StringSplitOptions.RemoveEmptyEntries));
        foreach (string tag in tags)
        {
            TagWithId? item = ParseTag(tag, tagIdMode);
            if (item is null)
            {
                continue;
            }
            result.Tags.Add(item);
        }
        return result;
    }

    private static TagWithId? ParseTag(string text, bool tagIdMode)
    {
        text = text.Trim();
        if (text.Length == 0)
        {
            return null;
        }
        if (!tagIdMode)
        {
            return new(text);
        }
        string[] tagPieces = text.Split('#', 2, StringSplitOptions.RemoveEmptyEntries);
        if (tagPieces.Length < 2)
        {
            return new(text);
        }
        if (!int.TryParse(tagPieces[0].Trim(), out int tagId))
        {
            return new(text);
        }
        text = tagPieces[1].Trim();
        if (text.Length == 0)
        {
            return null;
        }
        return new(text)
        {
            Id = tagId
        };
    }

    //
    // Utilities
    //

    private static string ToStandardString(string text)
    {
        return text.Trim().Replace('\r', '\n');
    }

    //
    // Types
    //

    private class ParsePropertyResult(TagWithId name)
    {
        public TagWithId Name = name;
        public List<TagWithId> Tags = [];
    };

    private class TagWithId(string content)
    {
        public int Id = -1;
        public string Content = content;

        public override bool Equals(object? obj)
        {
            if (obj is TagWithId tag)
            {
                return Content == tag.Content;
            }
            return false;
        }

        public override int GetHashCode()
        {
            return Content.GetHashCode();
        }

        public override string ToString()
        {
            return Content;
        }
    };
}
