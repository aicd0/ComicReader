using System.Collections.Generic;

namespace ComicReader.Utils.Lifecycle;

internal class EventBus
{
    public static readonly EventBus Default = new();
    private static readonly IMutableLiveData<object> sEmptyLiveData = new EmptyLiveData<object>();

    private readonly Dictionary<string, ILiveDataNoType> _topics = new();
    private bool _clearing = false;

    public IMutableLiveData<T> With<T>(string eventId)
    {
        if (_clearing)
        {
            return sEmptyLiveData as IMutableLiveData<T>;
        }

        if (_topics.TryGetValue(eventId, out ILiveDataNoType topic))
        {
            return topic as IMutableLiveData<T>;
        }

        var newTopic = new MutableLiveData<T>();
        _topics.Add(eventId, newTopic);
        return newTopic;
    }

    public IMutableLiveData<object> With(string eventId)
    {
        return With<object>(eventId);
    }

    public void Clear()
    {
        _clearing = true;
        foreach (ILiveDataNoType topic in _topics.Values)
        {
            topic.Clear();
        }
        _clearing = false;
        _topics.Clear();
    }
}
