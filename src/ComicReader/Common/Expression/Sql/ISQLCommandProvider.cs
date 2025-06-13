// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;

using ComicReader.SDK.Data.SqlHelpers;

namespace ComicReader.Common.Expression.Sql;

internal interface ISQLCommandProvider<K>
{
    ITable GetTable();

    ICondition CreateVariableCondition(IReadOnlyList<string> path);

    ICondition CreateListCondition(List<VariableOrValue> path);

    ICondition CreateComparisonCondition(VariableOrValue left, VariableOrValue right, ComparisonCondition.TypeEnum comparisonType);

    ICondition CreateInCondition(List<VariableOrValue> left, List<VariableOrValue> right);
}
