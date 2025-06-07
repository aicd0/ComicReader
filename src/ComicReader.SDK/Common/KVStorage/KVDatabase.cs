// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

namespace ComicReader.SDK.Common.KVStorage;

public static class KVDatabase
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
