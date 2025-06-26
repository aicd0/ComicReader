﻿// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

namespace ComicReader.Common.Lifecycle;

internal class EmptyEventBus : IEventBus
{
    public static readonly EmptyEventBus Instance = new();

    private EmptyEventBus() { }

    public void Clear()
    {
    }

    public IMutableLiveData<T> With<T>(string eventId)
    {
        return new EmptyLiveData<T>();
    }

    public IMutableLiveData<object> With(string eventId)
    {
        return new EmptyLiveData<object>();
    }
}
