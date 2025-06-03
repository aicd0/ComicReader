// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using ComicReader.SDK.Common.DebugTools;

namespace ComicReader.SDK.Data.AutoProperty.Presets;

public class ConverterProperty<A, Q, R, B>(IQRProperty<Q, R> source, Func<PropertyRequestContent<A>, PropertyRequestContent<Q>> requestConverter, Func<PropertyResponseContent<R>, PropertyResponseContent<B>> responseConverter) : AbsProperty<A, B, ConverterPropertyModel, IPropertyExtension>
{
    public override ConverterPropertyModel CreateModel()
    {
        return new ConverterPropertyModel();
    }

    public override List<IProperty> GetDependentProperties()
    {
        return [source];
    }

    public override void RearrangeRequests(PropertyContext<A, B, ConverterPropertyModel, IPropertyExtension> context)
    {
        ConverterPropertyModel model = context.Model;
        foreach (SealedPropertyRequest<A> serverRequest in context.NewRequests)
        {
            PropertyRequestContent<Q> convertedRequest;
            try
            {
                convertedRequest = requestConverter(serverRequest.RequestContent);
            }
            catch (Exception e)
            {
                Logger.AssertNotReachHere("DC4669BD138CDCBB", e);
                context.Respond(serverRequest.Id, PropertyResponseContent<B>.NewFailedResponse());
                continue;
            }
            SealedPropertyRequest<Q>? subRequest = context.Request(source, convertedRequest, OnResponse);
            if (subRequest is null)
            {
                context.Respond(serverRequest.Id, PropertyResponseContent<B>.NewFailedResponse());
                continue;
            }
            model.requests[subRequest.Id] = serverRequest.Id;
        }
    }

    public override void ProcessRequests(PropertyContext<A, B, ConverterPropertyModel, IPropertyExtension> context, IProcessCallback callback)
    {
        callback.PostCompletion(null);
    }

    private void OnResponse(PropertyContext<A, B, ConverterPropertyModel, IPropertyExtension> context, long id, PropertyResponseContent<R> response)
    {
        if (!context.Model.requests.Remove(id, out long originId))
        {
            Logger.AssertNotReachHere("");
            return;
        }
        PropertyResponseContent<B> convertedResponse;
        try
        {
            convertedResponse = responseConverter(response);
        }
        catch (Exception e)
        {
            Logger.AssertNotReachHere("F7A6288C8CB7584C", e);
            convertedResponse = PropertyResponseContent<B>.NewFailedResponse();
        }
        context.Respond(originId, convertedResponse);
    }
}
