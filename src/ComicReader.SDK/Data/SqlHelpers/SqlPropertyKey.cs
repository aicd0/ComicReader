// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using ComicReader.SDK.Common.AutoProperty;

namespace ComicReader.SDK.Data.SqlHelpers;

public class SqlPropertyKey<K, V>(IColumn<K> keyColumn, IColumn<V> valueColumn, K sqlKey, string resourceKey) : IRequestKey
{
    public readonly IColumn<K> KeyColumn = keyColumn;
    public readonly IColumn<V> ValueColumn = valueColumn;
    public readonly K SqlKey = sqlKey;
    public readonly string ResourceKey = resourceKey;
}
