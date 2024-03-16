using System.Collections.Generic;

namespace ComicReader.Utils
{
    internal class EventBus
    {
        public static readonly EventBus Default = new EventBus();
        private readonly Dictionary<string, object> _topics = new Dictionary<string, object>();

        public MutableLiveData<T> With<T>(string eventId)
        {
            if (_topics.TryGetValue(eventId, out object topic))
            {
                return topic as MutableLiveData<T>;
            }

            var newTopic = new MutableLiveData<T>();
            _topics.Add(eventId, newTopic);
            return newTopic;
        }

        public MutableLiveData<object> With(string eventId)
        {
            return With<object>(eventId);
        }
    }
}
