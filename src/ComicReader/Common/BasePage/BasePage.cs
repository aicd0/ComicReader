// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;

using ComicReader.Helpers.Navigation;

using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;

namespace ComicReader.Common.BasePage;

internal abstract class BasePage : StatefulPage
{
    private NavigationBundle _bundle;
    private PointerPoint _lastPointerPoint;

    public BasePage()
    {
        AddHandler(PointerPressedEvent, new PointerEventHandler(OnPagePointerPressed), true);
    }

    protected sealed override void OnStart(object p)
    {
        base.OnStart(p);
        _bundle = (NavigationBundle)p;
        OnStart(_bundle.Bundle);
    }

    protected virtual void OnStart(PageBundle bundle)
    {
    }

    protected void TransferAbilities(NavigationBundle bundle)
    {
        foreach (KeyValuePair<Type, IPageAbility> entry in _bundle.Abilities)
        {
            bundle.Abilities[entry.Key] = entry.Value;
        }
    }

    protected T GetAbility<T>() where T : class
    {
        if (_bundle.Abilities.TryGetValue(typeof(T), out IPageAbility value))
        {
            return (T)value;
        }

        return null;
    }

    protected bool CanHandleTapped()
    {
        if (_lastPointerPoint == null)
        {
            return true;
        }

        if (_lastPointerPoint.Properties.IsXButton1Pressed || _lastPointerPoint.Properties.IsXButton2Pressed)
        {
            return false;
        }

        return true;
    }

    private void OnPagePointerPressed(object sender, PointerRoutedEventArgs e)
    {
        _lastPointerPoint = e.GetCurrentPoint((UIElement)sender);
    }
}
