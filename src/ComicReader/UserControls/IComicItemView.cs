﻿// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using ComicReader.ViewModels;

namespace ComicReader.UserControls;

interface IComicItemView
{
    void Bind(ComicItemViewModel item);

    void Unbind();
}
