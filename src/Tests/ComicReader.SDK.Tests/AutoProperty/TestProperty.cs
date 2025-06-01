// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using ComicReader.SDK.Common.Threading;
using ComicReader.SDK.Data.AutoProperty;

namespace ComicReader.SDK.Tests.AutoProperty;

internal class TestProperty<Q, R>(Func<PropertyRequestContent<Q>, PropertyResponseContent<R>> serverFunc, bool rearrange, bool processOnServerThread) : AbsProperty<Q, R, TestPropertyModel<Q>, IPropertyExtension>
{
    private volatile bool _versionChanged;

    public void NotifyVersionChange()
    {
        _versionChanged = true;
    }

    public override TestPropertyModel<Q> CreateModel()
    {
        return new TestPropertyModel<Q>();
    }

    public override List<IProperty> GetDependentProperties()
    {
        return [];
    }

    public override void RearrangeRequests(PropertyContext<Q, R, TestPropertyModel<Q>, IPropertyExtension> context)
    {
        TestPropertyModel<Q> model = context.Model;
        foreach (SealedPropertyRequest<Q> serverRequest in context.NewRequests)
        {
            PropertyRequestContent<Q> request = serverRequest.RequestContent;
            if (_versionChanged)
            {
                context.Dependency.IncrementVersion(request.Key);
            }
            if (rearrange)
            {
                context.Respond(serverRequest.Id, serverFunc(serverRequest.RequestContent));
            }
            else
            {
                model.requests.Enqueue(serverRequest);
            }
        }
        _versionChanged = false;
    }

    public override void ProcessRequests(PropertyContext<Q, R, TestPropertyModel<Q>, IPropertyExtension> context, IProcessCallback callback)
    {
        if (processOnServerThread)
        {
            while (context.Model.requests.TryDequeue(out SealedPropertyRequest<Q>? request))
            {
                context.Respond(request.Id, serverFunc(request.RequestContent));
            }
            callback.PostCompletion(null);
        }
        else
        {
            TaskDispatcher.DefaultQueue.Submit("TestProperty", () =>
            {
                callback.PostCompletion(() =>
                {
                    while (context.Model.requests.TryDequeue(out SealedPropertyRequest<Q>? request))
                    {
                        context.Respond(request.Id, serverFunc(request.RequestContent));
                    }
                });
            });
        }
    }
}
