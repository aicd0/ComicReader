// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

namespace ComicReader.Common.Flow;

internal interface IFlow<T>
{
    public delegate void ValueChangedEventHandler(T value);
    public event ValueChangedEventHandler ValueChanged;

    T Get();
}
