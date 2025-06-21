// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using ComicReader.SDK.Common.AutoProperty;

namespace ComicReader.SDK.Data.SqlHelpers;

public class SqlPropertyKey<K, V>(IColumn<K> keyColumn, IColumn<V> valueColumn, K sqlKey, string resourceKey) : IRequestKey where K : notnull
{
    public readonly IColumn<K> KeyColumn = keyColumn;
    public readonly IColumn<V> ValueColumn = valueColumn;
    public readonly K SqlKey = sqlKey;
    public readonly string ResourceKey = resourceKey;

    public override string ToString()
    {
        return $"{KeyColumn.Name}/{ValueColumn.Name}/{SqlKey}";
    }

    public override bool Equals(object? obj)
    {
        if (obj is not SqlPropertyKey<K, V> other)
        {
            return false;
        }
        if (!KeyColumn.Equals(other.KeyColumn) || !ValueColumn.Equals(other.ValueColumn))
        {
            return false;
        }
        if (!EqualityComparer<K>.Default.Equals(SqlKey, other.SqlKey))
        {
            return false;
        }
        return true;
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(KeyColumn, ValueColumn, SqlKey);
    }
}
