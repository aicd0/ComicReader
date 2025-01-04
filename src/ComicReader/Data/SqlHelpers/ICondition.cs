// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

namespace ComicReader.Data.SqlHelpers;

internal interface ICondition
{
    string GetExpression(ICommandContext command);
}
