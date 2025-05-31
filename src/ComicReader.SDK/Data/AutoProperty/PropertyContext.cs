// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using ComicReader.SDK.Common.DebugTools;

namespace ComicReader.SDK.Data.AutoProperty;

public class PropertyContext<Q, R, M> : IQRPropertyContext<Q, R>
{
    private readonly IServerContext _context;
    private readonly AbsProperty<Q, R, M> _property;
    private readonly Dictionary<long, PropertyRequestContent<Q>> _ongoingRequests = [];

    private readonly List<ServerPropertyRequest<Q>> _newRequests = [];
    public IReadOnlyList<ServerPropertyRequest<Q>> NewRequests => _newRequests;

    private readonly M _model;
    public M Model => _model;

    public PropertyContext(IServerContext context, AbsProperty<Q, R, M> property)
    {
        _context = context;
        _property = property;
        _model = property.CreateModel();
    }

    public ServerPropertyRequest<A>? Request<A, B>(IQRProperty<A, B> target, PropertyRequestContent<A> request, Action<PropertyContext<Q, R, M>, long, PropertyResponseContent<B>> handler)
    {
        return _context.HandleRequest(_property, target, request, (p1, p2) =>
        {
            handler(this, p1, p2);
        });
    }

    public void Respond(long requestId, PropertyResponseContent<R> response)
    {
        _context.HandleRespond(_property, requestId, response);
        _ongoingRequests.Remove(requestId);
    }

    public void Redirect(long requestId, IQRProperty<Q, R> target)
    {
        if (_context.HandleRedirect(_property, requestId, target))
        {
            _ongoingRequests.Remove(requestId);
        }
    }

    public void ClearNewRequests()
    {
        _newRequests.Clear();
    }

    public void AddNewRequest(long id, PropertyRequestContent<Q> request)
    {
        _ongoingRequests.Add(id, request);
        _newRequests.Add(new ServerPropertyRequest<Q>(id, request));
    }

    public void RearrangeRequests()
    {
        _property.RearrangeRequests(this);
    }

    public void ProcessRequests(IProcessCallback callback)
    {
        _property.ProcessRequests(this, callback);
    }

    public void CancelRequest(long id)
    {
        if (!_ongoingRequests.TryGetValue(id, out PropertyRequestContent<Q>? actualRequest))
        {
            Logger.AssertNotReachHere("14672E06AAB0669E");
            return;
        }
        Respond(id, PropertyResponseContent<R>.NewFailedResponse());
    }
}
