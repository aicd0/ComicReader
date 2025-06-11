// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.Text;

using ComicReader.Common.Expression.Tokens;

namespace ComicReader.Common.Expression;

internal sealed class ExpressionToken
{
    public const int LEVEL_RAW = 0;
    public const int TYPE_RAW_KEYWORD = 0;
    public const int TYPE_RAW_LEFT_PARENTHESIS = 1;
    public const int TYPE_RAW_RIGHT_PARENTHESIS = 2;
    public const int TYPE_RAW_OPERATOR = 3;
    public const int TYPE_RAW_FUNC = 4;

    public const int LEVEL_INTERMEDIATE = 1;
    public const int TYPE_INTERMEDIATE_EXPRESSION_LIST = 0;

    public const int LEVEL_FINAL = 2;
    public const int TYPE_FINAL_EMPTY = 0;
    public const int TYPE_FINAL_FUNC = 1;
    public const int TYPE_FINAL_VALUE = 2;
    public const int TYPE_FINAL_VARIABLE = 3;

    private readonly ITokenExtra? _extra;

    public int Level;
    public int Type;

    public ITokenExtra? Extra => _extra;
    public RawKeywordTokenExtra RawKeywordExtra => (RawKeywordTokenExtra)_extra!;
    public RawOperatorTokenExtra RawOperatorExtra => (RawOperatorTokenExtra)_extra!;
    public RawFuncTokenExtra RawFuncExtra => (RawFuncTokenExtra)_extra!;
    public IntermediateExpressionListTokenExtra IntermediateExpressionListExtra => (IntermediateExpressionListTokenExtra)_extra!;
    public FinalFuncTokenExtra FinalFuncTokenExtra => (FinalFuncTokenExtra)_extra!;
    public FinalValueTokenExtra FinalValueExtra => (FinalValueTokenExtra)_extra!;
    public FinalVariableTokenExtra FinalVariableExtra => (FinalVariableTokenExtra)_extra!;

    private ExpressionToken(int level, int type, ITokenExtra? extra)
    {
        Level = level;
        Type = type;
        _extra = extra;
    }

    public override string ToString()
    {
        StringBuilder sb = new();
        _extra?.ToString(sb);
        return sb.ToString();
    }

    public static ExpressionToken CreateRawKeyword(string name)
    {
        return new ExpressionToken(LEVEL_RAW, TYPE_RAW_KEYWORD, new RawKeywordTokenExtra(name));
    }

    public static ExpressionToken CreateRawLeftParenthesis()
    {
        return new ExpressionToken(LEVEL_RAW, TYPE_RAW_LEFT_PARENTHESIS, null);
    }

    public static ExpressionToken CreateRawRightParenthesis()
    {
        return new ExpressionToken(LEVEL_RAW, TYPE_RAW_RIGHT_PARENTHESIS, null);
    }

    public static ExpressionToken CreateRawOperator(string name)
    {
        return new ExpressionToken(LEVEL_RAW, TYPE_RAW_OPERATOR, new RawOperatorTokenExtra(name));
    }

    public static ExpressionToken CreateRawFunc(string name)
    {
        return new ExpressionToken(LEVEL_RAW, TYPE_RAW_FUNC, new RawFuncTokenExtra(name));
    }

    public static ExpressionToken CreateIntermediateExpressionList(IEnumerable<ExpressionToken> expressions)
    {
        return new ExpressionToken(LEVEL_INTERMEDIATE, TYPE_INTERMEDIATE_EXPRESSION_LIST, new IntermediateExpressionListTokenExtra(expressions));
    }

    public static ExpressionToken CreateFinalEmpty()
    {
        return new ExpressionToken(LEVEL_FINAL, TYPE_FINAL_EMPTY, null);
    }

    public static ExpressionToken CreateFinalFunc(string name, IEnumerable<ExpressionToken> parameters)
    {
        return new ExpressionToken(LEVEL_FINAL, TYPE_FINAL_FUNC, new FinalFuncTokenExtra(name, parameters));
    }

    public static ExpressionToken CreateFinalValue(string value)
    {
        return new ExpressionToken(LEVEL_FINAL, TYPE_FINAL_VALUE, new FinalValueTokenExtra(FinalValueTokenExtra.TypeEnum.Keyword, value));
    }

    public static ExpressionToken CreateFinalNumberLiteral(string number)
    {
        return new ExpressionToken(LEVEL_FINAL, TYPE_FINAL_VALUE, new FinalValueTokenExtra(FinalValueTokenExtra.TypeEnum.NumberLiteral, number));
    }

    public static ExpressionToken CreateFinalStringLiteral(string value)
    {
        return new ExpressionToken(LEVEL_FINAL, TYPE_FINAL_VALUE, new FinalValueTokenExtra(FinalValueTokenExtra.TypeEnum.StringLiteral, value));
    }

    public static ExpressionToken CreateFinalVariable(string name)
    {
        return new ExpressionToken(LEVEL_FINAL, TYPE_FINAL_VARIABLE, new FinalVariableTokenExtra(name));
    }
}
