// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;

using Microsoft.UI.Xaml;

namespace ComicReader.Views.Reader;

internal class ReaderFrameManager
{
    //
    // Variables
    //

    private int _readyFrameIndex = -1;
    private readonly Dictionary<int, FrameInfo> _frameInfoDictionary = [];

    public delegate void FrameReadyHandler(int index);
    private FrameReadyHandler _frameReadyHandler;

    //
    // Methods
    //

    public void PutFrame(int index, FrameworkElement container, bool isReady)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(index);

        {
            if (!_frameInfoDictionary.TryGetValue(index, out FrameInfo frameInfo))
            {
                frameInfo = new FrameInfo();
                _frameInfoDictionary.Add(index, frameInfo);
            }
            frameInfo.IsReady = isReady;
            frameInfo.Container = container;
        }

        if (isReady)
        {
            IncreaseReadyIndex();
        }
        else
        {
            DecreaseReadyIndex(index);
        }
    }

    public void RemoveFrame(int index)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(index);

        if (_frameInfoDictionary.Remove(index))
        {
            DecreaseReadyIndex(index);
        }
    }

    public FrameworkElement GetContainer(int index)
    {
        if (_frameInfoDictionary.TryGetValue(index, out FrameInfo frameInfo))
        {
            if (frameInfo.IsReady)
            {
                return frameInfo.Container;
            }
        }
        return null;
    }

    public void ResetReadyIndex()
    {
        _readyFrameIndex = -1;
    }

    public void SetFrameReadyHandler(FrameReadyHandler handler)
    {
        _frameReadyHandler = handler;
    }

    private void IncreaseReadyIndex()
    {
        int readyFrameIndex = _readyFrameIndex + 1;
        for (; true; ++readyFrameIndex)
        {
            if (_frameInfoDictionary.TryGetValue(readyFrameIndex, out FrameInfo frameInfo))
            {
                if (!frameInfo.IsReady)
                {
                    break;
                }
            }
            else
            {
                break;
            }
        }
        --readyFrameIndex;

        if (readyFrameIndex == _readyFrameIndex)
        {
            return;
        }

        int oldFrameIndex = _readyFrameIndex;
        _readyFrameIndex = readyFrameIndex;

        for (int i = oldFrameIndex + 1; i <= readyFrameIndex; ++i)
        {
            if (_frameReadyHandler == null)
            {
                break;
            }
            _frameReadyHandler.Invoke(i);
        }
    }

    private void DecreaseReadyIndex(int index)
    {
        _readyFrameIndex = Math.Min(index - 1, _readyFrameIndex);
    }

    //
    // Structs
    //

    class FrameInfo
    {
        public bool IsReady { get; set; } = false;
        public FrameworkElement Container { get; set; }
    }
}
