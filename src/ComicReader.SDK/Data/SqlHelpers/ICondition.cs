// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

namespace ComicReader.SDK.Data.SqlHelpers;

public interface ICondition
{
    internal string GetExpression(ICommandContext command);
}
