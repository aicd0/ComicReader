// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using ComicReader.Data.Models;
using ComicReader.SDK.Common.ServiceManagement;

namespace ComicReader.Common.Services;

internal class DebugService : IDebugService
{
    public bool EnableSqliteDatabaseLog()
    {
        return DebugSwitchModel.Instance.SqliteLogEnabled;
    }
}
