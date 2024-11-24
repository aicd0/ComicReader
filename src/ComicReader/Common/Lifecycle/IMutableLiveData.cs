// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

namespace ComicReader.Common.Lifecycle;

internal interface IMutableLiveData<T> : ILiveData<T>
{
    void Emit(T value);
}
