// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;

using ComicReader.Common.Expression.Compiler;

namespace ComicReader.Common.Expression;

internal class ExpressionParser
{
    public static ExpressionToken Parse(string expression)
    {
        LinkedList<ExpressionToken> tokens = [];
        Tokenizer.Tokenize(expression, tokens);
        ExpressionToken token = Evaluator.Evaluate(tokens);
        token = Optimizer.Optimize(token);
        return token;
    }
}
