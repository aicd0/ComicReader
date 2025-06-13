// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;

using ComicReader.Common.Expression.Sql;
using ComicReader.Data.Tables;
using ComicReader.SDK.Data.SqlHelpers;

namespace ComicReader.Data.Models.Comic;

internal class ComicSQLCommandProvider : ISQLCommandProvider<long>
{
    public ICondition CreateComparisonCondition(VariableOrValue left, VariableOrValue right, ComparisonCondition.TypeEnum comparisonType)
    {
        return new BooleanCondition(false);
    }

    public ICondition CreateInCondition(List<VariableOrValue> left, List<VariableOrValue> right)
    {
        return new BooleanCondition(false);
    }

    public ICondition CreateListCondition(List<VariableOrValue> path)
    {
        return new BooleanCondition(false);
    }

    public ICondition CreateVariableCondition(IReadOnlyList<string> path)
    {
        return new BooleanCondition(false);
    }

    public ITable GetTable()
    {
        return ComicTable.Instance;
    }
}
