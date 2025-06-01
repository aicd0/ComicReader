// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using ComicReader.SDK.Common.Threading;
using ComicReader.SDK.Data.AutoProperty;

namespace ComicReader.SDK.Tests.AutoProperty;

internal class TestProperty<Q, R> : AbsProperty<Q, R, TestPropertyModel<Q>, IPropertyExtension>
{
    public volatile Func<PropertyRequestContent<Q>, PropertyResponseContent<R>> ServerFunc = (request) =>
    {
        return PropertyResponseContent<R>.NewFailedResponse();
    };

    public volatile bool Rearrange = true;
    public volatile bool ProcessOnServerThread = true;

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
            if (request.Type == RequestType.Modify)
            {
                context.Dependency.IncrementVersion(request.Key);
            }
            if (Rearrange)
            {
                context.Respond(serverRequest.Id, ServerFunc(serverRequest.RequestContent));
            }
            else
            {
                model.requests.Enqueue(serverRequest);
            }
        }
    }

    public override void ProcessRequests(PropertyContext<Q, R, TestPropertyModel<Q>, IPropertyExtension> context, IProcessCallback callback)
    {
        if (ProcessOnServerThread)
        {
            while (context.Model.requests.TryDequeue(out SealedPropertyRequest<Q>? request))
            {
                context.Respond(request.Id, ServerFunc(request.RequestContent));
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
                        context.Respond(request.Id, ServerFunc(request.RequestContent));
                    }
                });
            });
        }
    }
}
