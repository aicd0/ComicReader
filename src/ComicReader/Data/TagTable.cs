// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using ComicReader.Data.SqlHelpers;

namespace ComicReader.Data;

internal class TagTable : ITable
{
    public static TagTable Instance { get; } = new TagTable();

    public static Column ColumnContent { get; } = new("content");
    public static Column ColumnComicId { get; } = new("comic_id");
    public static Column ColumnTagCategoryId { get; } = new("cate_id");

    private TagTable() { }

    public string GetTableName()
    {
        return "tags";
    }
}
