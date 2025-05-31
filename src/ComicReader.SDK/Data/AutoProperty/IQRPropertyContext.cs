// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

namespace ComicReader.SDK.Data.AutoProperty;

public interface IQRPropertyContext<Q, R> : IPropertyContext
{
    void AddNewRequest(long id, PropertyRequestContent<Q> request);
}
