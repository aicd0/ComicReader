using ComicReader.Router;
using System;

namespace ComicReader.Views.Base;
internal abstract class BaseViewModel
{
    private WeakReference<NavigationBundle> _bundle;

    public virtual void OnStart()
    {
    }

    public virtual void OnResume()
    {
    }

    public virtual void OnPause()
    {
    }

    public void SetNavigationBundle(NavigationBundle bundle)
    {
        if (_bundle != null)
        {
            throw new InvalidOperationException();
        }

        _bundle = new WeakReference<NavigationBundle>(bundle);
    }

    protected T GetAbility<T>() where T : class
    {
        if (_bundle != null)
        {
            if (_bundle.TryGetTarget(out NavigationBundle bundle))
            {
                if (bundle.Abilities.TryGetValue(typeof(T), out object ability))
                {
                    return (T)ability;
                }
            }
        }

        return null;
    }
}
