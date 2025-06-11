// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System.Text;

namespace ComicReader.Common.Expression.Tokens;

class FinalVariableTokenExtra(string path) : ITokenExtra
{
    public readonly string Path = path;

    public void ToString(StringBuilder stringBuilder)
    {
        stringBuilder.Append(Path);
    }
}
