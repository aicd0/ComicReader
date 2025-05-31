// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

namespace ComicReader.SDK.Data.AutoProperty;

public abstract class AbsProperty<Q, R, M> : IQRProperty<Q, R>
{
    public abstract M CreateModel();

    public abstract void RearrangeRequests(PropertyContext<Q, R, M> context);

    public abstract void ProcessRequests(PropertyContext<Q, R, M> context, IProcessCallback callback);

    public IPropertyContext CreatePropertyContext(IServerContext context)
    {
        return new PropertyContext<Q, R, M>(context, this);
    }
}
