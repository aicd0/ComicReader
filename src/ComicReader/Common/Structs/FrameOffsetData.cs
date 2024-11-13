// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

namespace ComicReader.Common.Structs;

internal class FrameOffsetData
{
    public double ParallelBegin;
    public double ParallelCenter;
    public double ParallelEnd;
    public double PerpendicularCenter;

    public override string ToString()
    {
        return "{" +
            "PB=" + ParallelBegin.ToString() + "," +
            "PB=" + ParallelCenter.ToString() + "," +
            "PE=" + ParallelEnd.ToString() + "," +
            "PnC=" + PerpendicularCenter.ToString() + "}";
    }
}
