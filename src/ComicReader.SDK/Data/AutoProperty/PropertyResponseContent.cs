// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

namespace ComicReader.SDK.Data.AutoProperty;

public class PropertyResponseContent<V>
{
    public OperationResult Result { get; }
    public V? Value { get; }
    public IReadonlyResponseTracker? Tracker { get; }
    public int Version { get; }

    private PropertyResponseContent(OperationResult result, V? value, IReadonlyResponseTracker? tracker, int version)
    {
        Result = result;
        Value = value;
        Tracker = tracker;
        Version = version;
    }

    public PropertyResponseContent<V> WithTracker(IReadonlyResponseTracker? tracker, int version)
    {
        return new PropertyResponseContent<V>(Result, Value, tracker, version);
    }

    public static PropertyResponseContent<V> NewSuccessfulResponse()
    {
        return new(OperationResult.Successful, default, null, 0);
    }

    public static PropertyResponseContent<V> NewSuccessfulResponse(V? value)
    {
        return new(OperationResult.Successful, value, null, 0);
    }

    public static PropertyResponseContent<V> NewSuccessfulResponse(IReadonlyResponseTracker? tracker, int version)
    {
        return new(OperationResult.Successful, default, tracker, version);
    }

    public static PropertyResponseContent<V> NewSuccessfulResponse(V? value, IReadonlyResponseTracker? tracker, int version)
    {
        return new(OperationResult.Successful, value, tracker, version);
    }

    public static PropertyResponseContent<V> NewFailedResponse()
    {
        return new(OperationResult.PropertyError, default, null, 0);
    }

    public static PropertyResponseContent<V> NewFailedResponse(IReadonlyResponseTracker? tracker, int version)
    {
        return new(OperationResult.PropertyError, default, tracker, version);
    }

    internal static PropertyResponseContent<V> NewResponse(OperationResult result)
    {
        return new(result, default, null, 0);
    }
}
