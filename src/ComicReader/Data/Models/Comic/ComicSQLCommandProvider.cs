// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;

using ComicReader.Common.Expression;
using ComicReader.Common.Expression.Sql;
using ComicReader.Data.Tables;
using ComicReader.SDK.Data.SqlHelpers;

namespace ComicReader.Data.Models.Comic;

internal class ComicSQLCommandProvider : ISQLCommandProvider
{
    public const string VAR_TAG = "Tag";
    public const string VAR_TITLE = "Title";

    public ICondition CreateComparisonCondition(VariableOrValue left, VariableOrValue right, ComparisonTypeEnum comparisonType)
    {
        return new BooleanCondition(false);
    }

    public ICondition CreateInCondition(List<VariableOrValue> left, List<VariableOrValue> right)
    {
        return new BooleanCondition(false);
    }

    public ICondition CreateListCondition(List<VariableOrValue> path)
    {
        throw new ExpressionException("Cannot pass lists as an expression.");
    }

    public ICondition CreateVariableCondition(IReadOnlyList<string> path)
    {
        if (path.Count == 0)
        {
            throw new ExpressionException("Variable path cannot be empty.");
        }
        if (path.Count == 1)
        {
            return CreateVariableCondition(path[0]);
        }
        if (path.Count == 2)
        {
            return CreateVariableCondition(path[0], path[1]);
        }
        if (path.Count == 3)
        {
            return CreateVariableCondition(path[0], path[1], path[2]);
        }
        throw new ExpressionException("Unrecognized variable path.");
    }

    private static ICondition CreateVariableCondition(string path1)
    {
        return path1 switch
        {
            VAR_TAG => CreateHasTagCondition(),
            VAR_TITLE => new OrCondition([NotNullAndEmptyCondition(ComicTable.ColumnTitle1), NotNullAndEmptyCondition(ComicTable.ColumnTitle2)]),
            _ => throw new ExpressionException("Unrecognized variable path."),
        };
    }

    private static ICondition CreateVariableCondition(string path1, string path2)
    {
        return path1 switch
        {
            VAR_TAG => CreateHasTagCategoryCondition(path2),
            _ => throw new ExpressionException("Unrecognized variable path."),
        };
    }

    private static ICondition CreateVariableCondition(string path1, string path2, string path3)
    {
        return path1 switch
        {
            VAR_TAG => CreateHasTagInTagCategoryCondition(path2, path3),
            _ => throw new ExpressionException("Unrecognized variable path."),
        };
    }

    private static ICondition CreateHasTagCondition()
    {
        SelectCommand subquery = new(TagCategoryTable.Instance);
        subquery.PutQueryInt64(TagCategoryTable.ColumnComicId);
        subquery.Distinct();
        return new InCondition(ColumnOrValue.FromColumn(ComicTable.ColumnId), subquery);
    }

    private static ICondition CreateHasTagCategoryCondition(string category)
    {
        SelectCommand subquery = new(TagCategoryTable.Instance);
        subquery.AppendCondition(TagCategoryTable.ColumnName, category);
        subquery.PutQueryInt64(TagCategoryTable.ColumnComicId);
        subquery.Distinct();
        return new InCondition(ColumnOrValue.FromColumn(ComicTable.ColumnId), subquery);
    }

    private static ICondition CreateHasTagInTagCategoryCondition(string category, string tag)
    {
        SelectCommand subquery1 = new(TagCategoryTable.Instance);
        subquery1.AppendCondition(TagCategoryTable.ColumnName, category);
        subquery1.PutQueryInt64(TagCategoryTable.ColumnId);
        SelectCommand subquery2 = new(TagTable.Instance);
        subquery2.AppendCondition(TagTable.ColumnContent, tag);
        subquery2.AppendCondition(new InCondition(ColumnOrValue.FromColumn(TagTable.ColumnTagCategoryId), subquery1));
        subquery2.PutQueryInt64(TagTable.ColumnComicId);
        subquery2.Distinct();
        return new InCondition(ColumnOrValue.FromColumn(ComicTable.ColumnId), subquery2);
    }

    private static ICondition NotNullAndEmptyCondition(IColumnTypeless column)
    {
        return new AndCondition([
            new ComparisonCondition(ColumnOrValue.FromColumn(column), ColumnOrValue.FromValue(null), ComparisonCondition.TypeEnum.IsNot),
            new ComparisonCondition(ColumnOrValue.FromColumn(column), ColumnOrValue.FromValue(""), ComparisonCondition.TypeEnum.NotEqual),
        ]);
    }
}
