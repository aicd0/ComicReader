// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;

namespace ComicReader.ViewModels;

internal partial class ComicGroupViewModel(string groupName, IEnumerable<ComicItemViewModel> items, bool collapsed) : SimpleGroupViewModel<ComicItemViewModel>(groupName, items, collapsed)
{
}
