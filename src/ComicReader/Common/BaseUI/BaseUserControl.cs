// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using Microsoft.UI.Xaml.Controls;

namespace ComicReader.Common.BaseUI;

public partial class BaseUserControl : UserControl
{
    public StringResourceProvider StringResource { get; } = StringResourceProvider.Instance;
}
