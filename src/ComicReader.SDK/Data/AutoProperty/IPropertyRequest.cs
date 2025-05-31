// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

namespace ComicReader.SDK.Data.AutoProperty;

public interface IPropertyRequest
{
    long Id { get; }
    RequestState State { get; set; }
    IProperty Sender { get; }
    IProperty Receiver { get; }
}
