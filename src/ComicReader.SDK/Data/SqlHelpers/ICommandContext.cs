﻿// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

namespace ComicReader.SDK.Data.SqlHelpers;

internal interface ICommandContext
{
    string AppendParameter(object value);
}
