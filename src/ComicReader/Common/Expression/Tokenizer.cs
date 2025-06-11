// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.Text;

namespace ComicReader.Common.Expression;

class Tokenizer
{
    public static void Tokenize(string expression, LinkedList<ExpressionToken> tokens)
    {
        int index = 0;
        while (index < expression.Length)
        {
            char currentChar = expression[index];
            if (IsWhiteSpace(currentChar))
            {
                index++;
                continue;
            }
            if (IsNamingCharacter(currentChar))
            {
                ParseNaming(expression, tokens, ref index);
                continue;
            }
            if (IsDigit(currentChar))
            {
                ParseDigit(expression, tokens, ref index);
                continue;
            }
            switch (currentChar)
            {
                case '(':
                    tokens.AddLast(ExpressionToken.CreateRawLeftParenthesis());
                    index++;
                    continue;
                case ')':
                    tokens.AddLast(ExpressionToken.CreateRawRightParenthesis());
                    index++;
                    continue;
                case '"':
                    ParseQuatedString(expression, tokens, ref index);
                    continue;
                case '%':
                    ParsePercentSign(expression, tokens, ref index);
                    continue;
                case ',':
                case '=':
                    tokens.AddLast(ExpressionToken.CreateRawOperator(currentChar.ToString()));
                    index++;
                    continue;
                case '>':
                case '<':
                    ParseAngledQuote(expression, tokens, ref index);
                    continue;
                default:
                    break;
            }
            throw new ExpressionException($"Unexpected character '{currentChar}' at position {index}.");
        }
    }

    private static void ParseNaming(string expression, LinkedList<ExpressionToken> tokens, ref int index)
    {
        int state = 0;
        StringBuilder nameBuilder = new();
        while (true)
        {
            if (index >= expression.Length)
            {
                if (state == 0)
                {
                    throw new ExpressionException("Unexpected end of expression while parsing identifier.");
                }
                tokens.AddLast(ExpressionToken.CreateRawKeyword(nameBuilder.ToString()));
                break;
            }
            char currentChar = expression[index];
            switch (state)
            {
                case 0:
                    state = 1;
                    break;
                case 1:
                    if (IsNamingCharacter(currentChar) || IsDigit(currentChar))
                    {
                        break;
                    }
                    else if (currentChar == '(')
                    {
                        string name = nameBuilder.ToString();
                        if (IsKeyword(name))
                        {
                            tokens.AddLast(ExpressionToken.CreateRawKeyword(name));
                        }
                        else
                        {
                            tokens.AddLast(ExpressionToken.CreateRawFunc(name));
                        }
                        return;
                    }
                    else
                    {
                        string name = nameBuilder.ToString();
                        if (IsKeyword(name))
                        {
                            tokens.AddLast(ExpressionToken.CreateRawKeyword(name));
                        }
                        else
                        {
                            throw new ExpressionException($"Unknown identifier '{name}'.");
                        }
                        return;
                    }
                default:
                    throw new ExpressionException($"Invalid state {state} while parsing identifier.");
            }
            nameBuilder.Append(currentChar);
            index++;
        }
    }

    private static void ParseDigit(string expression, LinkedList<ExpressionToken> tokens, ref int index)
    {
        int state = 0;
        StringBuilder numberBuilder = new();
        while (true)
        {
            if (index >= expression.Length)
            {
                if (state == 0)
                {
                    throw new ExpressionException("Unexpected end of expression while parsing number.");
                }
                tokens.AddLast(ExpressionToken.CreateFinalNumberLiteral(numberBuilder.ToString()));
                break;
            }
            char currentChar = expression[index];
            switch (state)
            {
                case 0:
                    state = 1;
                    break;
                case 1:
                    if (IsDigit(currentChar))
                    {
                        break;
                    }
                    else if (currentChar == '.')
                    {
                        state = 2;
                        break;
                    }
                    else
                    {
                        tokens.AddLast(ExpressionToken.CreateFinalNumberLiteral(numberBuilder.ToString()));
                        return;
                    }
                case 2:
                    if (IsDigit(currentChar))
                    {
                        break;
                    }
                    else if (currentChar == '.')
                    {
                        throw new ExpressionException($"Unexpected character '{currentChar}' after decimal point in number.");
                    }
                    else
                    {
                        tokens.AddLast(ExpressionToken.CreateFinalNumberLiteral(numberBuilder.ToString()));
                        return;
                    }
                default:
                    throw new ExpressionException($"Invalid state {state} while parsing number.");
            }
            numberBuilder.Append(currentChar);
            index++;
        }
    }

    private static void ParseQuatedString(string expression, LinkedList<ExpressionToken> tokens, ref int index)
    {
        int state = 0;
        StringBuilder stringBuilder = new();
        index++; // Skip the opening quote
        while (true)
        {
            if (index >= expression.Length)
            {
                throw new ExpressionException("Unexpected end of expression while parsing quoted string.");
            }
            char currentChar = expression[index];
            switch (state)
            {
                case 0:
                    if (currentChar == '"')
                    {
                        tokens.AddLast(ExpressionToken.CreateFinalStringLiteral(stringBuilder.ToString()));
                        index++; // Skip the closing quote
                        return;
                    }
                    else if (currentChar == '\\')
                    {
                        state = 1; // Escape sequence
                    }
                    else
                    {
                        stringBuilder.Append(currentChar);
                    }
                    break;
                case 1:
                    stringBuilder.Append(currentChar);
                    state = 0; // Reset to normal state
                    break;
                default:
                    throw new ExpressionException($"Invalid state {state} while parsing quoted string.");
            }
            index++;
        }
    }

    private static void ParseAngledQuote(string expression, LinkedList<ExpressionToken> tokens, ref int index)
    {
        int state = 0;
        StringBuilder nameBuilder = new();
        while (true)
        {
            if (index >= expression.Length)
            {
                if (state == 0)
                {
                    throw new ExpressionException("Unexpected end of expression while parsing angled quote.");
                }
                tokens.AddLast(ExpressionToken.CreateRawOperator(nameBuilder.ToString()));
                break;
            }
            char currentChar = expression[index];
            switch (state)
            {
                case 0:
                    state = 1;
                    break;
                case 1:
                    if (currentChar == '=')
                    {
                        state = 2;
                    }
                    else
                    {
                        tokens.AddLast(ExpressionToken.CreateRawOperator(nameBuilder.ToString()));
                        return;
                    }
                    break;
                case 2:
                    tokens.AddLast(ExpressionToken.CreateRawOperator(nameBuilder.ToString()));
                    return;
                default:
                    throw new ExpressionException($"Invalid state {state} while parsing angled quoted string.");
            }
            nameBuilder.Append(currentChar);
            index++;
        }
    }

    private static void ParsePercentSign(string expression, LinkedList<ExpressionToken> tokens, ref int index)
    {
        int state = 0;
        StringBuilder variableBuilder = new();
        index++;
        while (true)
        {
            if (index >= expression.Length)
            {
                if (state == 0)
                {
                    throw new ExpressionException("Unexpected end of expression while parsing variable.");
                }
                tokens.AddLast(ExpressionToken.CreateFinalVariable(variableBuilder.ToString()));
                break;
            }
            char currentChar = expression[index];
            switch (state)
            {
                case 0:
                    if (IsNamingCharacter(currentChar))
                    {
                        variableBuilder.Append(currentChar);
                        state = 1;
                    }
                    else
                    {
                        throw new ExpressionException($"Unexpected character '{currentChar}' after '%'.");
                    }
                    break;
                case 1:
                    if (IsNamingCharacter(currentChar) || IsDigit(currentChar))
                    {
                        variableBuilder.Append(currentChar);
                    }
                    else if (currentChar == '.')
                    {
                        state = 0; // Allow dot for path-like variables
                        variableBuilder.Append(currentChar);
                    }
                    else
                    {
                        tokens.AddLast(ExpressionToken.CreateFinalVariable(variableBuilder.ToString()));
                        return;
                    }
                    break;
                default:
                    throw new ExpressionException($"Invalid state {state} while parsing variable.");
            }
            index++;
        }
    }

    private static bool IsWhiteSpace(char c)
    {
        return char.IsWhiteSpace(c);
    }

    private static bool IsNamingCharacter(char c)
    {
        return !char.IsWhiteSpace(c) && (char.IsLetter(c) || c == '_' || !char.IsAscii(c));
    }

    private static bool IsDigit(char c)
    {
        return char.IsDigit(c);
    }

    private static bool IsKeyword(string name)
    {
        return name.ToUpper() switch
        {
            "IN" or "NOT" or "AND" or "OR" or "TRUE" or "FALSE" => true,
            _ => false,
        };
    }
}
