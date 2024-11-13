// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

namespace ComicReader.Utils.Lifecycle;

internal interface IObserver<T>
{
    void OnChanged(T value);
}
