// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

namespace ComicReader.SDK.Data.AutoProperty;

public class ResponseTracker : IReadonlyResponseTracker
{
    private bool _calculating = false;
    private int _version = 0;
    private readonly List<IReadonlyResponseTracker> _subTrackers = [];

    public int Version
    {
        get
        {
            if (_calculating)
            {
                return 0;
            }
            _calculating = true;
            try
            {
                int version = _version;
                foreach (ResponseTracker subTracker in _subTrackers)
                {
                    version += subTracker.Version;
                }
                return version;
            }
            finally
            {
                _calculating = false;
            }
        }
    }

    internal ResponseTracker() { }

    public void IncrementVersion()
    {
        _version++;
    }

    public void UpdateTrackers(IEnumerable<IReadonlyResponseTracker> subTrackers, int version)
    {
        int oldVersion = Version;
        _subTrackers.Clear();
        _subTrackers.AddRange(subTrackers);
        _version = oldVersion - version;
    }
}
