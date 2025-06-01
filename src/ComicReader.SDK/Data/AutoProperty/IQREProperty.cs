// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

namespace ComicReader.SDK.Data.AutoProperty;

public interface IQREProperty<Q, R, E> : IQRProperty<Q, R>, IEProperty<E> where E : IPropertyExtension
{
}
