// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using Microsoft.UI.Xaml.Controls;

namespace ComicReader.Common.PageBase;

public partial class BaseContentDialog : ContentDialog
{
    public StringResourceProvider StringResource { get; } = StringResourceProvider.Instance;
}
