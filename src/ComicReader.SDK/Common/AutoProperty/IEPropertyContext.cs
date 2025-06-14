// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

namespace ComicReader.SDK.Common.AutoProperty;

public interface IEPropertyContext<E> : IPropertyContext where E : IPropertyExtension
{
    internal void RegisterExtension(E extension);
}
