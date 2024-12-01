// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;

namespace ComicReader.Common.Flow;

internal interface IFlowOperation<T>
{
    void RegisterObserver(Action observer);

    T CalculateValue();

    T CalculateIntermediateValue();
}
