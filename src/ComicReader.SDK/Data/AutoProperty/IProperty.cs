// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

namespace ComicReader.SDK.Data.AutoProperty;

public interface IProperty
{
    internal IPropertyContext CreatePropertyContext(IServerContext context);
}
