using System.Collections.Generic;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml;

namespace ComicReader.Utils
{
    internal class LiveData<T>
    {
        private readonly List<Observer<T>> _observers = new List<Observer<T>>();
        private T _value;
        private int _version = 0;
        private bool _dispatchingValue = false;
        private bool _dispatchInvalidated = false;

        public void Observe(Page owner, Observer<T> observer, bool sticky = false)
        {
            if (owner == null || observer == null)
            {
                return;
            }
            if (!owner.IsLoaded)
            {
                return;
            }
            void unloadedHandler(object sender, RoutedEventArgs e)
            {
                if ((sender as Page).IsLoaded)
                {
                    return;
                }
                _observers.Remove(observer);
                owner.Unloaded -= unloadedHandler;
            }
            owner.Unloaded += unloadedHandler;
            _observers.Add(observer);
            if (sticky && _version > 0)
            {
                DispatchValue(observer, _value);
            }
        }

        public void Emit(T value)
        {
            _value = value;
            _version++;
            DispatchValue(null, value);
        }

        private void DispatchValue(Observer<T> initiator, T value)
        {
            if (_dispatchingValue)
            {
                _dispatchInvalidated = true;
                return;
            }
            _dispatchingValue = true;
            do
            {
                _dispatchInvalidated = false;
                if (initiator != null)
                {
                    ConsiderNotify(initiator, value);
                }
                else
                {
                    foreach (Observer<T> observer in _observers)
                    {
                        ConsiderNotify(observer, value);
                        if (_dispatchInvalidated)
                        {
                            break;
                        }
                    }
                }
            } while (_dispatchInvalidated);
            _dispatchingValue = false;
        }

        private void ConsiderNotify(Observer<T> observer, T value)
        {
            observer(value);
        }
    }

    internal delegate void Observer<T>(T value);
}
