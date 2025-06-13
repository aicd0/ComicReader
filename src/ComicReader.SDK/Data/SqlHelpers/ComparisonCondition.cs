// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System.Text;

namespace ComicReader.SDK.Data.SqlHelpers;

public class ComparisonCondition : ICondition
{
    private readonly ColumnOrValue _left;
    private readonly ColumnOrValue _right;
    private readonly TypeEnum _type;

    public ComparisonCondition(ColumnOrValue left, ColumnOrValue right, TypeEnum type = TypeEnum.Equal)
    {
        _left = left;
        _right = right;
        _type = type;
    }

    string ICondition.GetExpression(ICommandContext command)
    {
        StringBuilder sb = new();
        _left.AppendToCommand(sb, command);
        string operatorString = _type switch
        {
            TypeEnum.NotEqual => "<>",
            TypeEnum.GreaterThan => ">",
            TypeEnum.LessThan => "<",
            TypeEnum.GreaterThanOrEqual => ">=",
            TypeEnum.LessThanOrEqual => "<=",
            _ => "=",
        };
        sb.Append(' ').Append(operatorString).Append(' ');
        _right.AppendToCommand(sb, command);
        return sb.ToString();
    }

    public enum TypeEnum
    {
        Equal,
        NotEqual,
        GreaterThan,
        LessThan,
        GreaterThanOrEqual,
        LessThanOrEqual,
    }
}
