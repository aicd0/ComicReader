// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

namespace ComicReader.SDK.Data.AutoProperty;

public abstract class AbsProperty<Q, R, M, E> : IQREProperty<Q, R, E> where E : IPropertyExtension
{
    public abstract M CreateModel();

    public abstract List<IProperty> GetDependentProperties();

    public abstract void RearrangeRequests(PropertyContext<Q, R, M, E> context);

    public abstract void ProcessRequests(PropertyContext<Q, R, M, E> context, IProcessCallback callback);

    IPropertyContext IProperty.CreatePropertyContext(IServerContext context, DependencyToken dependency)
    {
        return new PropertyContext<Q, R, M, E>(context, this, dependency);
    }
}
