// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

#nullable disable

using System.Text.Json;

using ComicReader.SDK.Common.DebugTools;
using ComicReader.SDK.Common.Threading;

namespace ComicReader.SDK.Data;

public abstract class JsonDatabase<T> where T : class
{
    private const string TAG = nameof(JsonDatabase<T>);

    private readonly string _fileName;
    private readonly ReaderWriterLock _lock = new();
    private volatile T _jsonModel;

    private readonly JsonSerializerOptions _serializerOptions = new()
    {
        WriteIndented = DebugUtils.DebugMode,
        Encoder = DebugUtils.DebugMode ? System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping : System.Text.Encodings.Web.JavaScriptEncoder.Default,
    };

    protected JsonDatabase(string fileName)
    {
        _fileName = fileName;
    }

    protected abstract T CreateModel();

    protected async Task<R> Read<R>(Func<T, R> func)
    {
        await Initialize();
        _lock.AcquireReaderLock(Timeout.Infinite);
        try
        {
            return func(_jsonModel);
        }
        finally
        {
            _lock.ReleaseReaderLock();
        }
    }

    protected async Task<R> Write<R>(Func<T, R> func)
    {
        await Initialize();
        _lock.AcquireWriterLock(Timeout.Infinite);
        try
        {
            return func(_jsonModel);
        }
        finally
        {
            _lock.ReleaseWriterLock();
        }
    }

    protected async Task Write(T model)
    {
        ArgumentNullException.ThrowIfNull(model, nameof(model));

        await Initialize();
        _lock.AcquireWriterLock(Timeout.Infinite);
        try
        {
            _jsonModel = model;
        }
        finally
        {
            _lock.ReleaseWriterLock();
        }
    }

    protected async Task Save()
    {
        await Read(delegate (T model)
        {
            T clonedModel = CloneModel(model);
            if (clonedModel != null)
            {
                TaskDispatcher.DefaultQueue.Submit($"{TAG}#{nameof(Save)}", delegate
                {
                    string json = JsonSerializer.Serialize(clonedModel, _serializerOptions);
                    SimpleConfigDatabase.Instance.TryPutConfig(_fileName, json).Wait();
                });
            }
            return true;
        });
    }

    protected T CloneModel(T model)
    {
        string json = JsonSerializer.Serialize(model, _serializerOptions);
        try
        {
            return JsonSerializer.Deserialize<T>(json, _serializerOptions);
        }
        catch (JsonException ex)
        {
            Logger.F(TAG, nameof(CloneModel), ex);
            return null;
        }
    }

    private async Task Initialize()
    {
        if (_jsonModel != null)
        {
            return;
        }

        string json = await SimpleConfigDatabase.Instance.TryGetConfig(_fileName);
        T jsonModel = null;
        if (json != null)
        {
            try
            {
                jsonModel = JsonSerializer.Deserialize<T>(json, _serializerOptions);
            }
            catch (JsonException ex)
            {
                Logger.F(TAG, nameof(Initialize), ex);
            }
        }
        jsonModel ??= CreateModel();

        _lock.AcquireWriterLock(Timeout.Infinite);
        try
        {
            if (_jsonModel != null)
            {
                return;
            }
            _jsonModel = jsonModel;
        }
        finally
        {
            _lock.ReleaseWriterLock();
        }
    }
}
