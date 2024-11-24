// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;

namespace ComicReader.Common;

internal class ZoomCoefficientResult
{
    public double FitWidth;
    public double FitHeight;

    public double Min()
    {
        return Math.Min(FitWidth, FitHeight);
    }

    public double Max()
    {
        return Math.Max(FitWidth, FitHeight);
    }
}
