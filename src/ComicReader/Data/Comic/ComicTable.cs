// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using ComicReader.Data.SqlHelpers;

namespace ComicReader.Data.Comic;

internal class ComicTable : ITable
{
    public static ComicTable Instance { get; } = new ComicTable();

    public static Column ColumnId { get; } = new("id");
    public static Column ColumnType { get; } = new("type");
    public static Column ColumnLocation { get; } = new("location");
    public static Column ColumnTitle1 { get; } = new("title1");
    public static Column ColumnTitle2 { get; } = new("title2");
    public static Column ColumnHidden { get; } = new("hidden");
    public static Column ColumnRating { get; } = new("rating");
    public static Column ColumnProgress { get; } = new("progress");
    public static Column ColumnLastVisit { get; } = new("last_visit");
    public static Column ColumnLastPosition { get; } = new("last_pos");
    public static Column ColumnCoverCacheKey { get; } = new("cover_cache_key");
    public static Column ColumnDescription { get; } = new("description");

    private ComicTable() { }

    public string GetTableName()
    {
        return "comics";
    }
}
