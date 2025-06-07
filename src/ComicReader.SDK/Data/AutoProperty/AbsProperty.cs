// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

namespace ComicReader.SDK.Data.AutoProperty;

public abstract class AbsProperty<K, V, M, E> : IKVEProperty<K, V, E> where K : IRequestKey where E : IPropertyExtension
{
    public abstract M CreateModel();

    public abstract LockResource GetLockResource(K key, LockType type);

    public abstract void RearrangeRequests(PropertyContext<K, V, M, E> context);

    public abstract void ProcessRequests(PropertyContext<K, V, M, E> context, IProcessCallback callback);

    IPropertyContext IProperty.CreatePropertyContext(IServerContext context)
    {
        return new PropertyContext<K, V, M, E>(context, this);
    }
}
