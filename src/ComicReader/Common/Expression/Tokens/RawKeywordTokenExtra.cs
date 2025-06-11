// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System.Text;

namespace ComicReader.Common.Expression.Tokens;

class RawKeywordTokenExtra(string name) : ITokenExtra
{
    public readonly string Name = name.ToUpper();

    public void ToString(StringBuilder stringBuilder)
    {
        stringBuilder.Append(Name);
    }
}
