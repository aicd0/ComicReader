// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

namespace ComicReader.SDK.Data.AutoProperty.Presets;

public interface IValueObserverExtension<T> : IPropertyExtension
{
    void UpdateValue(string key, T? value);
}
