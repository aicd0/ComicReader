// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

using ComicReader.Common.DebugTools;
using ComicReader.Common.Threading;

namespace ComicReader.Data;

abstract class JsonDatabase<T> where T : class
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

    protected R Read<R>(Func<T, R> func)
    {
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

    protected R Write<R>(Func<T, R> func)
    {
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

    protected void Write(T model)
    {
        ArgumentNullException.ThrowIfNull(model, nameof(model));

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

    protected async Task<bool> TryInitialize()
    {
        if (_jsonModel != null)
        {
            return true;
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
                Logger.F(TAG, nameof(TryInitialize), ex);
            }
        }
        jsonModel ??= CreateModel();

        _lock.AcquireWriterLock(Timeout.Infinite);
        try
        {
            if (_jsonModel != null)
            {
                return true;
            }
            _jsonModel = jsonModel;
        }
        finally
        {
            _lock.ReleaseWriterLock();
        }

        return true;
    }

    protected void Save()
    {
        Read(delegate (T model)
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

    protected void CloneFrom(T model)
    {
        Write(delegate (T m)
        {
            m = CloneModel(model);
            if (m != null)
            {
                _jsonModel = m;
            }
            return true;
        });
    }
}
