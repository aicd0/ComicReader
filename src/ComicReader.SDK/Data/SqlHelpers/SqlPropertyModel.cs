// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using ComicReader.SDK.Common.AutoProperty;

namespace ComicReader.SDK.Data.SqlHelpers;

public class SqlPropertyModel<K, V> where K : notnull
{
    internal readonly List<SealedPropertyRequest<SqlPropertyKey<K, V>, V>> requests = [];
}
