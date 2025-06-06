// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using ComicReader.SDK.Common.Threading;
using ComicReader.SDK.Data.AutoProperty;

namespace ComicReader.SDK.Tests.AutoProperty;

internal class TestProperty<V> : AbsProperty<TestPropertyKey, V, TestPropertyModel<V>, IPropertyExtension>
{
    public volatile Func<PropertyRequestContent<TestPropertyKey, V>, PropertyResponseContent<V>> ServerFunc = (request) =>
    {
        return PropertyResponseContent<V>.NewFailedResponse();
    };

    public volatile bool Hang = false;
    public volatile bool Rearrange = true;
    public volatile bool ProcessOnServerThread = true;

    public override TestPropertyModel<V> CreateModel()
    {
        return new TestPropertyModel<V>();
    }

    public override LockResource GetLockResource(TestPropertyKey key, LockType type)
    {
        LockResource resource = new();
        LockResource actualResource = new();
        actualResource.Type = type;
        resource.Children[key.Name] = actualResource;
        return resource;
    }

    public override void RearrangeRequests(PropertyContext<TestPropertyKey, V, TestPropertyModel<V>, IPropertyExtension> context)
    {
        if (Hang)
        {
            return;
        }

        TestPropertyModel<V> model = context.Model;

        foreach (SealedPropertyRequest<TestPropertyKey, V> serverRequest in context.NewRequests)
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

    public override void ProcessRequests(PropertyContext<TestPropertyKey, V, TestPropertyModel<V>, IPropertyExtension> context, IProcessCallback callback)
    {
        if (ProcessOnServerThread)
        {
            while (context.Model.requests.TryDequeue(out SealedPropertyRequest<TestPropertyKey, V>? request))
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
                    while (context.Model.requests.TryDequeue(out SealedPropertyRequest<TestPropertyKey, V>? request))
                    {
                        Respond(context, request);
                    }
                });
            });
        }
    }

    private void Respond(PropertyContext<TestPropertyKey, V, TestPropertyModel<V>, IPropertyExtension> context, SealedPropertyRequest<TestPropertyKey, V> request)
    {
        PropertyResponseContent<V> response = ServerFunc(request.RequestContent);
        ResponseTracker tracker = context.TrackerManager.GetOrAddTracker(request.RequestContent.Key);
        if (request.RequestContent.Type == RequestType.Modify && response.Result == RequestResult.Successful)
        {
            context.TrackerManager.IncrementVersion(request.RequestContent.Key);
        }
        response = response.WithTracker(tracker, tracker.Version);
        context.Respond(request.Id, response);
    }
}
