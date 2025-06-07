// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using ComicReader.SDK.Common.Threading;
using ComicReader.SDK.Data.AutoProperty;

namespace ComicReader.SDK.Tests.AutoProperty;

internal class TestProperty<V> : AbsProperty<TestPropertyKey, V, TestPropertyModel<V>, IPropertyExtension>
{
    public volatile Func<List<PropertyRequestContent<TestPropertyKey, V>>, List<PropertyResponseContent<V>>>? BatchServerFunc = null;
    public volatile Func<PropertyRequestContent<TestPropertyKey, V>, PropertyResponseContent<V>>? ServerFunc = null;
    public volatile bool Hang = false;
    public volatile bool Rearrange = true;
    public volatile bool ProcessOnServerThread = true;

    private readonly ITaskDispatcher _dispatcher = TaskDispatcher.Factory.NewThreadPool("Test");

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
            model.requests.Enqueue(serverRequest);
        }

        if (Rearrange)
        {
            Respond(context);
        }
    }

    public override void ProcessRequests(PropertyContext<TestPropertyKey, V, TestPropertyModel<V>, IPropertyExtension> context, IProcessCallback callback)
    {
        if (ProcessOnServerThread)
        {
            Respond(context);
            callback.PostCompletion(null);
        }
        else
        {
            _dispatcher.Submit("TestProperty", () =>
            {
                Thread.Sleep(Random.Shared.Next(1, 100));
                callback.PostCompletion(() =>
                {
                    Respond(context);
                });
            });
        }
    }

    private void Respond(PropertyContext<TestPropertyKey, V, TestPropertyModel<V>, IPropertyExtension> context)
    {
        List<SealedPropertyRequest<TestPropertyKey, V>> batchRequests = [];
        while (context.Model.requests.TryDequeue(out SealedPropertyRequest<TestPropertyKey, V>? request))
        {
            batchRequests.Add(request);
        }
        List<PropertyResponseContent<V>> batchResponses = BatchServerFunc?.Invoke(batchRequests.ConvertAll(x => x.RequestContent)) ?? [];
        int responseCount = Math.Min(batchResponses.Count, batchRequests.Count);
        for (int i = 0; i < responseCount; i++)
        {
            SealedPropertyRequest<TestPropertyKey, V> request = batchRequests[i];
            PropertyResponseContent<V> response = batchResponses[i];
            Respond(context, request, response);
        }
        for (int i = responseCount; i < batchRequests.Count; i++)
        {
            SealedPropertyRequest<TestPropertyKey, V> request = batchRequests[i];
            PropertyResponseContent<V> response = ServerFunc?.Invoke(request.RequestContent) ?? PropertyResponseContent<V>.NewFailedResponse();
            Respond(context, request, response);
        }
    }

    private void Respond(PropertyContext<TestPropertyKey, V, TestPropertyModel<V>, IPropertyExtension> context, SealedPropertyRequest<TestPropertyKey, V> request, PropertyResponseContent<V> response)
    {
        ResponseTracker tracker = context.TrackerManager.GetOrAddTracker(request.RequestContent.Key);
        if (request.RequestContent.Type == RequestType.Modify && response.Result == OperationResult.Successful)
        {
            context.TrackerManager.IncrementVersion(request.RequestContent.Key);
        }
        response = response.WithTracker(tracker, tracker.Version);
        context.Respond(request.Id, response);
    }
}
