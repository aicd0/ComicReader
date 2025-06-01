// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using ComicReader.SDK.Common.DebugTools;

namespace ComicReader.SDK.Data.AutoProperty.Presets;

public class ConvertProperty<A, Q, R, B>(IQRProperty<Q, R> source, Func<A?, Q> requestConverter, Func<R?, B> responseConverter) : AbsProperty<A, B, ConvertPropertyModel, IPropertyExtension>
{
    public override ConvertPropertyModel CreateModel()
    {
        return new ConvertPropertyModel();
    }

    public override List<IProperty> GetDependentProperties()
    {
        return [source];
    }

    public override void RearrangeRequests(PropertyContext<A, B, ConvertPropertyModel, IPropertyExtension> context)
    {
        ConvertPropertyModel model = context.Model;
        foreach (SealedPropertyRequest<A> serverRequest in context.NewRequests)
        {
            PropertyRequestContent<A> request = serverRequest.RequestContent;
            PropertyRequestContent<Q> convertedRequest = request.WithValue(requestConverter(request.Value));
            SealedPropertyRequest<Q>? subRequest = context.Request(source, convertedRequest, OnResponse);
            if (subRequest is null)
            {
                context.Respond(serverRequest.Id, PropertyResponseContent<B>.NewFailedResponse());
                continue;
            }
            model.requests[subRequest.Id] = serverRequest.Id;
        }
    }

    public override void ProcessRequests(PropertyContext<A, B, ConvertPropertyModel, IPropertyExtension> context, IProcessCallback callback)
    {
        callback.PostCompletion(null);
    }

    private void OnResponse(PropertyContext<A, B, ConvertPropertyModel, IPropertyExtension> context, long id, PropertyResponseContent<R> response)
    {
        if (!context.Model.requests.Remove(id, out long originId))
        {
            Logger.AssertNotReachHere("");
            return;
        }
        PropertyResponseContent<B> convertedResponse;
        if (response.Result == RequestResult.Successful)
        {
            convertedResponse = PropertyResponseContent<B>.NewSuccessfulResponse(responseConverter(response.Value));
        }
        else
        {
            convertedResponse = PropertyResponseContent<B>.NewFailedResponse();
        }
        context.Respond(originId, convertedResponse);
    }
}
