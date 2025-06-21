// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

namespace ComicReader.SDK.Common.AutoProperty;

public class ResponseTrackerManager<K> where K : IRequestKey
{
    readonly Dictionary<K, WeakReference<ResponseTracker>> _trackers = [];
    long _lastCleanupTime = 0;

    internal ResponseTrackerManager() { }

    public void IncrementVersion(K key)
    {
        CleanupIfNeeded();
        if (_trackers.TryGetValue(key, out WeakReference<ResponseTracker>? trackerRef))
        {
            if (trackerRef.TryGetTarget(out ResponseTracker? tracker))
            {
                tracker.IncrementVersion();
            }
            else
            {
                _trackers.Remove(key);
            }
        }
    }

    public ResponseTracker GetOrAddTracker(K key)
    {
        CleanupIfNeeded();
        if (_trackers.TryGetValue(key, out WeakReference<ResponseTracker>? trackerRef) && trackerRef.TryGetTarget(out ResponseTracker? tracker))
        {
            return tracker;
        }
        ResponseTracker newTracker = new();
        _trackers[key] = new WeakReference<ResponseTracker>(newTracker);
        return newTracker;
    }

    private void CleanupIfNeeded()
    {
        if (_trackers.Count <= 256)
        {
            return;
        }
        long tick = GetTick();
        if (tick - _lastCleanupTime < 30000)
        {
            return;
        }
        _lastCleanupTime = tick;

        var kvpToRemove = new List<KeyValuePair<K, WeakReference<ResponseTracker>>>();
        foreach (KeyValuePair<K, WeakReference<ResponseTracker>> kvp in _trackers)
        {
            if (!kvp.Value.TryGetTarget(out _))
            {
                kvpToRemove.Add(kvp);
            }
        }
        foreach (KeyValuePair<K, WeakReference<ResponseTracker>> kvp in kvpToRemove)
        {
            _trackers.Remove(kvp.Key);
        }
    }

    private static long GetTick()
    {
        return Environment.TickCount64;
    }
}
