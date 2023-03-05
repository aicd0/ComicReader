using System.Collections.Generic;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace ComicReader.Utils
{
    internal class EventBus
    {
        public static readonly EventBus Default = new EventBus();
        private readonly Dictionary<string, object> _topics = new Dictionary<string, object>();

        public LiveData<T> With<T>(string eventId)
        {
            if (_topics.TryGetValue(eventId, out var topic))
            {
                return topic as LiveData<T>;
            }
            var newTopic = new LiveData<T>();
            _topics.Add(eventId, newTopic);
            return newTopic;
        }

        public LiveData<object> With(string eventId)
        {
            return With<object>(eventId);
        }
    }
}
