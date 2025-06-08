// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;

using ComicReader.Data.Tables;
using ComicReader.SDK.Common.AutoProperty;
using ComicReader.SDK.Common.AutoProperty.Presets;
using ComicReader.SDK.Common.AutoProperty.Utils;
using ComicReader.SDK.Common.Threading;
using ComicReader.SDK.Data.SqlHelpers;

namespace ComicReader.Data.Models.Comic;

internal class ComicPropertyRepository
{
    public static readonly ComicPropertyRepository Instance = new();

    private readonly ITaskDispatcher _databaseDispatcher = TaskDispatcher.Factory.NewQueue("ComicDataQueue");
    private readonly PropertyServer _server = new("ComicPropertyRepository");

    private SqlProperty<ComicTable, long, int> CompletionStateSql { get; }
    private ConverterProperty<ComicIdKey, SqlPropertyKey<long, int>, int, ComicData.CompletionStateEnum> CompletionStateConverter { get; }
    private MemoryCacheProperty<ComicIdKey, ComicData.CompletionStateEnum> CompletionStateCache { get; }
    public SimplePropertyOperator<long, ComicIdKey, ComicData.CompletionStateEnum> CompletionStateOperator { get; }

    private ComicPropertyRepository()
    {
        CompletionStateSql = new(_databaseDispatcher, SqlDatabaseManager.MainDatabase, ComicTable.Instance);
        CompletionStateConverter = new(CompletionStateSql, (k) => new(ComicTable.ColumnId, ComicTable.ColumnCompletionState, k.Id, k.Id.ToString()),
            (v) => (int)v, (r) => r.WithValue(ParseCompletionState(r.Value)));
        CompletionStateCache = new(CompletionStateConverter);
        CompletionStateOperator = new(_server, CompletionStateCache, (k) => new(k));
    }

    public ITaskDispatcher GetDatabaseDispatcher()
    {
        return _databaseDispatcher;
    }

    public static ComicData.CompletionStateEnum ParseCompletionState(int value)
    {
        if (Enum.IsDefined(typeof(ComicData.CompletionStateEnum), value))
        {
            return (ComicData.CompletionStateEnum)value;
        }
        return ComicData.CompletionStateEnum.NotStarted;
    }
}
