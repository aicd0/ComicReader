using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;

namespace ComicReader.Utils
{
    internal class LiveData<T>
    {
        private readonly List<IObserver<T>> _observers = new List<IObserver<T>>();
        private T _value;
        private int _version = 0;
        private bool _dispatchingValue = false;
        private bool _dispatchInvalidated = false;

        public LiveData()
        {
            _value = default;
        }

        public LiveData(T initial)
        {
            _value = initial;
        }

        public void Observe(Page owner, Action<T> observer)
        {
            var wrapper = new Observer<T>(observer);
            Observe(owner, wrapper);
        }

        public void ObserveSticky(Page owner, Action<T> observer)
        {
            var wrapper = new Observer<T>(observer);
            ObserveSticky(owner, wrapper);
        }

        public void Observe(Page owner, IObserver<T> observer)
        {
            ObserveInternal(owner, observer, false);
        }

        public void ObserveSticky(Page owner, IObserver<T> observer)
        {
            ObserveInternal(owner, observer, true);
        }

        public void Emit(T value)
        {
            _value = value;
            _version++;
            DispatchValue(null, value);
        }

        public T GetValue()
        {
            return _value;
        }

        private void ObserveInternal(Page owner, IObserver<T> observer, bool sticky)
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

        private void DispatchValue(IObserver<T> initiator, T value)
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
                    foreach (IObserver<T> observer in _observers)
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

        private void ConsiderNotify(IObserver<T> observer, T value)
        {
            observer.OnChanged(value);
        }

        private class Observer<U> : IObserver<U>
        {
            private readonly Action<U> _action;

            public Observer(Action<U> action)
            {
                _action = action;
            }

            public void OnChanged(U value)
            {
                _action(value);
            }
        }
    }

    internal interface IObserver<T>
    {
        void OnChanged(T value);
    }
}
