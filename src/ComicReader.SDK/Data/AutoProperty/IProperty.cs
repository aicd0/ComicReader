// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

namespace ComicReader.SDK.Data.AutoProperty;

public interface IProperty
{
    IPropertyContext CreatePropertyContext(IServerContext context);
}
