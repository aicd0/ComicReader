﻿// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using ComicReader.SDK.Data.SqlHelpers;

namespace ComicReader.Data.Tables;

internal class TagCategoryTable : ITable
{
    public static TagCategoryTable Instance { get; } = new TagCategoryTable();

    public static Int64Column ColumnId { get; } = new("id");
    public static StringColumn ColumnName { get; } = new("name");
    public static Int64Column ColumnComicId { get; } = new("comic_id");

    private TagCategoryTable() { }

    public string GetTableName()
    {
        return "tag_categories";
    }
}
