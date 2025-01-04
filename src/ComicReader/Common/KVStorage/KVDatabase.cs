// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;

namespace ComicReader.Common.KVStorage;

internal static class KVDatabase
{
    private static readonly Lazy<KVDatabaseMethod> sDefaultMethod = new(delegate
    {
        return new KVDatabaseMethodCache(KVDatabaseMethodLiteDB.GetInstance());
    });

    public static KVDatabaseMethod GetDefaultMethod()
    {
        return sDefaultMethod.Value;
    }
}
