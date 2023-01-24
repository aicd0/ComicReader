using System.Collections.Generic;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace ComicReader.Utils
{
    internal class EventBus
    {
        private readonly Dictionary<string, object> _topics = new Dictionary<string, object>();

        public Topic<T> With<T>(string eventId)
        {
            if (_topics.TryGetValue(eventId, out var topic))
            {
                return topic as Topic<T>;
            }
            var newTopic = new Topic<T>();
            _topics.Add(eventId, newTopic);
            return newTopic;
        }

        private static EventBus _instance;

        public static EventBus Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = new EventBus();
                }
                return _instance;
            }
        }

        public class Topic<T>
        {
            private List<Observer<T>> _observers = new List<Observer<T>>();
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
                RoutedEventHandler unloadedHandler = null;
                unloadedHandler = delegate
                {
                    _observers.Remove(observer);
                    owner.Unloaded -= unloadedHandler;
                };
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

        public delegate void Observer<T>(T value);
    }
}
