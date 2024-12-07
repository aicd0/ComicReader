// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

namespace ComicReader.Data.SqlHelpers;

internal class Column
{
    private readonly string _name;

    public Column(string name)
    {
        _name = name;
    }

    public string Name => _name;
}
