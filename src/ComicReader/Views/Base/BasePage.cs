using ComicReader.Router;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using System;
using System.Collections.Generic;

namespace ComicReader.Views.Base;
internal abstract class BasePage<VM> : StatefulPage where VM : BaseViewModel, new()
{
    private NavigationBundle _bundle;
    private PointerPoint _lastPointerPoint;

    protected VM ViewModel { get; } = new VM();

    public BasePage()
    {
        AddHandler(PointerPressedEvent, new PointerEventHandler(OnPagePointerPressed), true);
    }

    protected sealed override void OnStart(object p)
    {
        base.OnStart(p);
        _bundle = (NavigationBundle)p;
        ViewModel.SetNavigationBundle(_bundle);
        OnStart(_bundle.Bundle);
    }

    protected virtual void OnStart(PageBundle bundle)
    {
        ViewModel.OnStart();
    }

    protected override void OnResume()
    {
        base.OnResume();
        ViewModel.OnResume();
    }

    protected override void OnPause()
    {
        base.OnPause();
        ViewModel.OnPause();
    }

    protected void TransferAbilities(NavigationBundle bundle)
    {
        foreach (KeyValuePair<Type, object> entry in _bundle.Abilities)
        {
            bundle.Abilities[entry.Key] = entry.Value;
        }
    }

    protected T GetAbility<T>() where T : class
    {
        if (_bundle.Abilities.TryGetValue(typeof(T), out object value))
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
