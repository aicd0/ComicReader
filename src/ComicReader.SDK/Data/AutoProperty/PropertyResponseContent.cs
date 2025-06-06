// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

namespace ComicReader.SDK.Data.AutoProperty;

public class PropertyResponseContent<V>
{
    public RequestResult Result { get; }
    public V? Value { get; }
    public IReadonlyResponseTracker? Tracker { get; }
    public int Version { get; }

    private PropertyResponseContent(RequestResult result, V? value, IReadonlyResponseTracker? tracker, int version)
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
        return new(RequestResult.Successful, default, null, 0);
    }

    public static PropertyResponseContent<V> NewSuccessfulResponse(V? value)
    {
        return new(RequestResult.Successful, value, null, 0);
    }

    public static PropertyResponseContent<V> NewSuccessfulResponse(IReadonlyResponseTracker? tracker, int version)
    {
        return new(RequestResult.Successful, default, tracker, version);
    }

    public static PropertyResponseContent<V> NewSuccessfulResponse(V? value, IReadonlyResponseTracker? tracker, int version)
    {
        return new(RequestResult.Successful, value, tracker, version);
    }

    public static PropertyResponseContent<V> NewFailedResponse()
    {
        return new(RequestResult.Failed, default, null, 0);
    }
}
