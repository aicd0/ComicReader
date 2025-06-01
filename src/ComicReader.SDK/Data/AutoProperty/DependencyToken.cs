// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

namespace ComicReader.SDK.Data.AutoProperty;

public class DependencyToken
{
    private readonly List<DependencyToken> _tokens = [];
    private readonly Dictionary<string, int> _versions = [];

    public void IncrementVersion(string key)
    {
        if (_versions.TryGetValue(key, out int version))
        {
            _versions[key] = version + 1;
        }
        else
        {
            _versions.Add(key, 1);
        }
    }

    public int GetDependencyVersion(string key)
    {
        int sum = 0;
        foreach (DependencyToken token in _tokens)
        {
            if (token._versions.TryGetValue(key, out int version))
            {
                sum += version;
            }
        }
        return sum;
    }

    internal void SetTokens(List<DependencyToken> tokens)
    {
        _tokens.Clear();
        _tokens.AddRange(tokens);
    }

    internal List<DependencyToken> GetTokens()
    {
        return _tokens;
    }
}
