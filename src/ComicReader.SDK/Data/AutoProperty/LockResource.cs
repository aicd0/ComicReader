// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

namespace ComicReader.SDK.Data.AutoProperty;

public class LockResource
{
    public LockType Type { get; set; } = LockType.None;
    public Dictionary<string, LockResource> Children { get; set; } = [];

    public LockResource Clone()
    {
        LockResource clone = new()
        {
            Type = Type
        };
        foreach (KeyValuePair<string, LockResource> child in Children)
        {
            clone.Children[child.Key] = child.Value.Clone();
        }
        return clone;
    }

    public void Merge(LockResource other)
    {
        if (other.Type > Type)
        {
            Type = other.Type; // Use the stronger lock type
        }
        foreach (KeyValuePair<string, LockResource> child in other.Children)
        {
            if (!Children.TryGetValue(child.Key, out LockResource? existingChild))
            {
                Children[child.Key] = child.Value.Clone(); // Add new child resource
            }
            else
            {
                existingChild.Merge(child.Value); // Merge with existing child resource
            }
        }
    }

    public bool Conflicts(LockResource other)
    {
        if ((Type == LockType.Write && other.Type != LockType.None) || (other.Type == LockType.Write && Type != LockType.None))
        {
            return true;
        }
        foreach (KeyValuePair<string, LockResource> otherChild in other.Children)
        {
            if (Children.TryGetValue(otherChild.Key, out LockResource? child))
            {
                if (child.Conflicts(otherChild.Value))
                {
                    return true;
                }
            }
        }
        return false;
    }
}
