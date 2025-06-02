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

    public volatile bool Hang = false;
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
        if (Hang)
        {
            return;
        }

        TestPropertyModel<Q> model = context.Model;

        foreach (SealedPropertyRequest<Q> serverRequest in context.NewRequests)
        {
            if (Rearrange)
            {
                Respond(context, serverRequest);
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
                Respond(context, request);
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
                        Respond(context, request);
                    }
                });
            });
        }
    }

    private void Respond(PropertyContext<Q, R, TestPropertyModel<Q>, IPropertyExtension> context, SealedPropertyRequest<Q> request)
    {
        PropertyResponseContent<R> response = ServerFunc(request.RequestContent);
        if (request.RequestContent.Type == RequestType.Modify && response.Result == RequestResult.Successful)
        {
            context.Dependency.IncrementVersion(request.RequestContent.Key);
        }
        context.Respond(request.Id, response);
    }
}
