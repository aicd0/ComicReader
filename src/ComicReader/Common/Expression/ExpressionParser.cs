// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;

namespace ComicReader.Common.Expression;

internal class ExpressionParser
{
    public static ExpressionToken Parse(string expression)
    {
        LinkedList<ExpressionToken> tokens = [];
        Tokenizer.Tokenize(expression, tokens);
        return Evaluate(tokens);
    }

    public string TranslateToSQLCondition(ExpressionToken token, ExpressionDataContext dataContext)
    {
        return "";
    }

    private static ExpressionToken Evaluate(LinkedList<ExpressionToken> tokens)
    {
        ExpressionToken token = EvaluateParenthses(tokens);
        if (token.Level != ExpressionToken.LEVEL_FINAL)
        {
            throw new ExpressionException("Expression is invalid.");
        }
        return token;
    }

    private static ExpressionToken EvaluateParenthses(LinkedList<ExpressionToken> tokens)
    {
        LinkedList<ExpressionToken> currentTokens = [];
        Stack<LinkedList<ExpressionToken>> parenthesesStack = [];
        LinkedListNode<ExpressionToken>? node = tokens.First;
        while (node != null)
        {
            ExpressionToken token = node.Value;
            node = node.Next;
            if (token.Level == ExpressionToken.LEVEL_RAW && token.Type == ExpressionToken.TYPE_RAW_LEFT_PARENTHESIS)
            {
                parenthesesStack.Push(currentTokens);
                currentTokens = [];
            }
            else if (token.Level == ExpressionToken.LEVEL_RAW && token.Type == ExpressionToken.TYPE_RAW_RIGHT_PARENTHESIS)
            {
                if (parenthesesStack.Count == 0)
                {
                    throw new ExpressionException("Unmatched right parenthesis.");
                }
                ExpressionToken evaluated = EvaluateParenthsesLess(currentTokens);
                currentTokens = parenthesesStack.Pop();
                currentTokens.AddLast(evaluated);
            }
            else
            {
                currentTokens.AddLast(token);
            }
        }
        if (parenthesesStack.Count > 0)
        {
            throw new ExpressionException("Unmatched left parenthesis.");
        }
        return EvaluateParenthsesLess(currentTokens);
    }

    private static ExpressionToken EvaluateParenthsesLess(LinkedList<ExpressionToken> tokens)
    {
        if (tokens.Count == 0)
        {
            return ExpressionToken.CreateFinalEmpty();
        }

        // Parse keywords TRUE FALSE
        {
            LinkedListNode<ExpressionToken>? node = tokens.First;
            while (node != null)
            {
                LinkedListNode<ExpressionToken>? nextNode = node.Next;
                ExpressionToken token = node.Value;
                if (token.Level == ExpressionToken.LEVEL_RAW && token.Type == ExpressionToken.TYPE_RAW_KEYWORD && (token.RawKeywordExtra.Name == "TRUE" || token.RawKeywordExtra.Name == "FALSE"))
                {
                    LinkedListNode<ExpressionToken> newNode = tokens.AddBefore(node, ExpressionToken.CreateFinalValue(token.RawKeywordExtra.Name));
                    tokens.Remove(node);
                    node = newNode;
                    nextNode = node.Next;
                }
                node = nextNode;
            }
        }

        // Parse functions
        {
            LinkedListNode<ExpressionToken>? node = tokens.First;
            while (node != null)
            {
                LinkedListNode<ExpressionToken>? nextNode = node.Next;
                ExpressionToken token = node.Value;
                if (token.Level == ExpressionToken.LEVEL_RAW && token.Type == ExpressionToken.TYPE_RAW_FUNC)
                {
                    if (nextNode == null)
                    {
                        throw new ExpressionException("Function call must be followed by a left parenthesis.");
                    }
                    ExpressionToken nextToken = nextNode.Value;
                    if (nextToken.Level == ExpressionToken.LEVEL_INTERMEDIATE)
                    {
                        if (nextToken.Type == ExpressionToken.TYPE_INTERMEDIATE_EXPRESSION_LIST)
                        {
                            LinkedListNode<ExpressionToken> newNode = tokens.AddBefore(node, ExpressionToken.CreateFinalFunc(token.RawFuncExtra.Name, nextToken.IntermediateExpressionListExtra.Expressions));
                            tokens.Remove(node);
                            tokens.Remove(nextNode);
                            node = newNode;
                            nextNode = node.Next;
                        }
                        else
                        {
                            throw new ExpressionException($"Unexpected token after function name: {nextToken}.");
                        }
                    }
                    else if (nextToken.Level == ExpressionToken.LEVEL_FINAL)
                    {
                        if (nextToken.Type == ExpressionToken.TYPE_FINAL_EMPTY)
                        {
                            LinkedListNode<ExpressionToken> newNode = tokens.AddBefore(node, ExpressionToken.CreateFinalFunc(token.RawFuncExtra.Name, []));
                            tokens.Remove(node);
                            tokens.Remove(nextNode);
                            node = newNode;
                            nextNode = node.Next;
                        }
                        else
                        {
                            LinkedListNode<ExpressionToken> newNode = tokens.AddBefore(node, ExpressionToken.CreateFinalFunc(token.RawFuncExtra.Name, [nextToken]));
                            tokens.Remove(node);
                            tokens.Remove(nextNode);
                            node = newNode;
                            nextNode = node.Next;
                        }
                    }
                    else
                    {
                        throw new ExpressionException($"Unexpected token after function name: {nextToken}.");
                    }
                }
                node = nextNode;
            }
        }

        // Parse operators = > < >= <=
        {
            LinkedListNode<ExpressionToken>? node = tokens.First;
            while (node != null)
            {
                LinkedListNode<ExpressionToken>? nextNode = node.Next;
                ExpressionToken token = node.Value;
                if (token.Level == ExpressionToken.LEVEL_RAW && token.Type == ExpressionToken.TYPE_RAW_OPERATOR && IsComparisonOperator(token.RawOperatorExtra.Name))
                {
                    LinkedListNode<ExpressionToken>? previousNode = node.Previous;
                    if (previousNode == null || nextNode == null)
                    {
                        throw new ExpressionException($"Operator '{token.RawOperatorExtra.Name}' must be surrounded by tokens on both sides.");
                    }
                    ExpressionToken previousToken = previousNode.Value;
                    ExpressionToken nextToken = nextNode.Value;
                    if (!IsValidFinalToken(previousToken) || !IsValidFinalToken(nextToken))
                    {
                        throw new ExpressionException($"Operator '{token.RawOperatorExtra.Name}' must be surrounded by valid tokens on both sides.");
                    }
                    LinkedListNode<ExpressionToken> newNode = tokens.AddBefore(previousNode, ExpressionToken.CreateFinalFunc(token.RawOperatorExtra.Name, [previousToken, nextToken]));
                    tokens.Remove(previousNode);
                    tokens.Remove(node);
                    tokens.Remove(nextNode);
                    node = newNode;
                    nextNode = node.Next;
                }
                node = nextNode;
            }
        }

        // Parse keyword IN
        {
            LinkedListNode<ExpressionToken>? node = tokens.First;
            while (node != null)
            {
                LinkedListNode<ExpressionToken>? nextNode = node.Next;
                ExpressionToken token = node.Value;
                if (token.Level == ExpressionToken.LEVEL_RAW && token.Type == ExpressionToken.TYPE_RAW_KEYWORD && token.RawKeywordExtra.Name == "IN")
                {
                    LinkedListNode<ExpressionToken>? previousNode = node.Previous;
                    if (previousNode == null || nextNode == null)
                    {
                        throw new ExpressionException($"Keyword '{token.RawKeywordExtra.Name}' must be surrounded by tokens on both sides.");
                    }
                    ExpressionToken previousToken = previousNode.Value;
                    ExpressionToken nextToken = nextNode.Value;
                    List<ExpressionToken> expressions = [];

                    if (IsValidFinalToken(previousToken))
                    {
                        expressions.Add(previousToken);
                    }
                    else
                    {
                        throw new ExpressionException($"Keyword '{token.RawKeywordExtra.Name}' must be preceded by a valid token.");
                    }

                    if (IsValidFinalToken(nextToken))
                    {
                        expressions.Add(nextToken);
                    }
                    else if (nextToken.Level == ExpressionToken.LEVEL_INTERMEDIATE && nextToken.Type == ExpressionToken.TYPE_INTERMEDIATE_EXPRESSION_LIST)
                    {
                        expressions.AddRange(nextToken.IntermediateExpressionListExtra.Expressions);
                    }
                    else
                    {
                        throw new ExpressionException($"Unexpected token after keyword '{token.RawKeywordExtra.Name}': {nextToken}.");
                    }

                    LinkedListNode<ExpressionToken> newNode = tokens.AddBefore(previousNode, ExpressionToken.CreateFinalFunc(token.RawKeywordExtra.Name, expressions));
                    tokens.Remove(previousNode);
                    tokens.Remove(node);
                    tokens.Remove(nextNode);
                    node = newNode;
                    nextNode = node.Next;
                }
                node = nextNode;
            }
        }

        // Parse keyword NOT
        {
            LinkedListNode<ExpressionToken>? node = tokens.Last;
            while (node != null)
            {
                LinkedListNode<ExpressionToken>? previousNode = node.Previous;
                ExpressionToken token = node.Value;
                if (token.Level == ExpressionToken.LEVEL_RAW && token.Type == ExpressionToken.TYPE_RAW_KEYWORD && token.RawKeywordExtra.Name == "NOT")
                {
                    LinkedListNode<ExpressionToken>? nextNode = node.Next ?? throw new ExpressionException($"Keyword '{token.RawKeywordExtra.Name}' must be followed by a valid token.");
                    ExpressionToken nextToken = nextNode.Value;
                    if (IsValidFinalToken(nextToken))
                    {
                        LinkedListNode<ExpressionToken> newNode = tokens.AddBefore(node, ExpressionToken.CreateFinalFunc(token.RawKeywordExtra.Name, [nextToken]));
                        tokens.Remove(node);
                        tokens.Remove(nextNode);
                        node = newNode;
                        previousNode = node.Previous;
                    }
                    else
                    {
                        throw new ExpressionException($"Unexpected token after keyword '{token.RawKeywordExtra.Name}': {nextToken}.");
                    }
                }
                node = previousNode;
            }
        }

        // Parse keywords AND OR
        {
            LinkedListNode<ExpressionToken>? node = tokens.First;
            while (node != null)
            {
                LinkedListNode<ExpressionToken>? nextNode = node.Next;
                ExpressionToken token = node.Value;
                if (token.Level == ExpressionToken.LEVEL_RAW && token.Type == ExpressionToken.TYPE_RAW_KEYWORD && (token.RawKeywordExtra.Name == "AND" || token.RawKeywordExtra.Name == "OR"))
                {
                    LinkedListNode<ExpressionToken>? previousNode = node.Previous;
                    if (previousNode == null || nextNode == null)
                    {
                        throw new ExpressionException($"Keyword '{token.RawKeywordExtra.Name}' must be surrounded by tokens on both sides.");
                    }
                    ExpressionToken previousToken = previousNode.Value;
                    ExpressionToken nextToken = nextNode.Value;
                    if (!IsValidFinalToken(previousToken) || !IsValidFinalToken(nextToken))
                    {
                        throw new ExpressionException($"Keyword '{token.RawKeywordExtra.Name}' must be surrounded by final tokens on both sides.");
                    }
                    LinkedListNode<ExpressionToken> newNode = tokens.AddBefore(previousNode, ExpressionToken.CreateFinalFunc(token.RawKeywordExtra.Name, [previousToken, nextToken]));
                    tokens.Remove(previousNode);
                    tokens.Remove(node);
                    tokens.Remove(nextNode);
                    node = newNode;
                    nextNode = node.Next;
                }
                node = nextNode;
            }
        }

        // Parse comma
        {
            LinkedListNode<ExpressionToken>? node = tokens.First;
            while (node != null)
            {
                LinkedListNode<ExpressionToken>? nextNode = node.Next;
                ExpressionToken token = node.Value;
                if (token.Level == ExpressionToken.LEVEL_RAW && token.Type == ExpressionToken.TYPE_RAW_OPERATOR && token.RawOperatorExtra.Name == ",")
                {
                    LinkedListNode<ExpressionToken>? previousNode = node.Previous;
                    if (previousNode == null || nextNode == null)
                    {
                        throw new ExpressionException($"Operator '{token.RawOperatorExtra.Name}' must be surrounded by tokens on both sides.");
                    }
                    ExpressionToken previousToken = previousNode.Value;
                    ExpressionToken nextToken = nextNode.Value;
                    List<ExpressionToken> expressions = [];

                    if (IsValidFinalToken(previousToken))
                    {
                        expressions.Add(previousToken);
                    }
                    else if (previousToken.Level == ExpressionToken.LEVEL_INTERMEDIATE && previousToken.Type == ExpressionToken.TYPE_INTERMEDIATE_EXPRESSION_LIST)
                    {
                        expressions.AddRange(previousToken.IntermediateExpressionListExtra.Expressions);
                    }
                    else
                    {
                        throw new ExpressionException($"Operator '{token.RawOperatorExtra.Name}' must be preceded by a valid token.");
                    }

                    if (IsValidFinalToken(nextToken))
                    {
                        expressions.Add(nextToken);
                    }
                    else if (nextToken.Level == ExpressionToken.LEVEL_INTERMEDIATE && nextToken.Type == ExpressionToken.TYPE_INTERMEDIATE_EXPRESSION_LIST)
                    {
                        expressions.AddRange(nextToken.IntermediateExpressionListExtra.Expressions);
                    }
                    else
                    {
                        throw new ExpressionException($"Operator '{token.RawOperatorExtra.Name}' must be followed by a valid token.");
                    }

                    LinkedListNode<ExpressionToken> newNode = tokens.AddBefore(previousNode, ExpressionToken.CreateIntermediateExpressionList(expressions));
                    tokens.Remove(previousNode);
                    tokens.Remove(node);
                    tokens.Remove(nextNode);
                    node = newNode;
                    nextNode = node.Next;
                }
                node = nextNode;
            }
        }

        LinkedListNode<ExpressionToken>? firstNode = tokens.First;
        if (firstNode is null)
        {
            return ExpressionToken.CreateFinalEmpty();
        }

        if (tokens.Count != 1)
        {
            throw new ExpressionException("Expression must be reduced to a single token.");
        }

        return firstNode.Value;
    }

    private static bool IsComparisonOperator(string name)
    {
        return name == "=" || name == ">" || name == "<" || name == ">=" || name == "<=";
    }

    private static bool IsValidFinalToken(ExpressionToken token)
    {
        return token.Level == ExpressionToken.LEVEL_FINAL && token.Type != ExpressionToken.TYPE_FINAL_EMPTY;
    }
}
