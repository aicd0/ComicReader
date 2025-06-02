// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;

using ComicReader.SDK.Common.DebugTools;

namespace ComicReader.SDK.Data.AutoProperty;

internal class RequestManager
{
    private readonly Dictionary<long, IPropertyRequest> _requests = [];
    private readonly Dictionary<IProperty, HashSet<IPropertyRequest>> _senders = [];
    private readonly Dictionary<IProperty, HashSet<IPropertyRequest>> _receivers = [];
    private readonly Dictionary<IProperty, List<IPropertyRequest>> _processingRequestSnapshot = [];

    public int RequestCount => _requests.Count;

    public void AddRequest(IPropertyRequest request)
    {
        _requests.Add(request.Id, request);

        {
            if (!_senders.TryGetValue(request.Sender, out HashSet<IPropertyRequest>? requests))
            {
                requests = [];
                _senders[request.Sender] = requests;
            }
            requests.Add(request);
        }

        {
            if (!_receivers.TryGetValue(request.Receiver, out HashSet<IPropertyRequest>? requests))
            {
                requests = [];
                _receivers[request.Receiver] = requests;
            }
            requests.Add(request);
        }
    }

    public void RemoveRequest(long id)
    {
        if (!_requests.Remove(id, out IPropertyRequest? request))
        {
            Logger.AssertNotReachHere("BF889E9BB8839C33");
            return;
        }

        {
            if (_senders.TryGetValue(request.Sender, out HashSet<IPropertyRequest>? requests))
            {
                if (!requests.Remove(request))
                {
                    Logger.AssertNotReachHere("BE1CE5E44D831EFC");
                }
            }
            else
            {
                Logger.AssertNotReachHere("24D4436894E7283A");
            }
        }

        {
            if (_receivers.TryGetValue(request.Receiver, out HashSet<IPropertyRequest>? requests))
            {
                if (!requests.Remove(request))
                {
                    Logger.AssertNotReachHere("88BB49A5FFBE5DE4");
                }
            }
            else
            {
                Logger.AssertNotReachHere("316D3B3D613B5631");
            }
        }
    }

    public bool TryGetRequest(long id, [MaybeNullWhen(false)] out IPropertyRequest request)
    {
        return _requests.TryGetValue(id, out request);
    }

    public IReadOnlyList<IProperty> StartProcess()
    {
        _processingRequestSnapshot.Clear();
        foreach (KeyValuePair<IProperty, HashSet<IPropertyRequest>> pair in _receivers)
        {
            IProperty receiver = pair.Key;
            if (!_senders.ContainsKey(receiver))
            {
                _processingRequestSnapshot.Add(receiver, [.. pair.Value]);
            }
        }
        return [.. _receivers.Keys];
    }

    public void EndProcess(IProperty property, out IReadOnlyList<IPropertyRequest> cancellingRequests)
    {
        List<IPropertyRequest> mutableCancellingRequests = [];
        cancellingRequests = mutableCancellingRequests;
        if (_processingRequestSnapshot.Remove(property, out List<IPropertyRequest>? requestSnapshot))
        {
            if (!_senders.ContainsKey(property))
            {
                if (_receivers.TryGetValue(property, out HashSet<IPropertyRequest>? requests))
                {
                    foreach (IPropertyRequest request in requestSnapshot)
                    {
                        if (requests.Contains(request))
                        {
                            mutableCancellingRequests.Add(request);
                        }
                    }
                }
            }
        }
    }
}
