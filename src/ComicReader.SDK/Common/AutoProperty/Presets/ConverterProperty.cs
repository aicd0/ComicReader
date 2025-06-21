// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using ComicReader.SDK.Common.DebugTools;

namespace ComicReader.SDK.Common.AutoProperty.Presets;

public class ConverterProperty<K, A, B, V>(IKVProperty<A, B> source, Func<K, A> keyConverter, Func<V?, B?> valueConverter, Func<PropertyResponseContent<B>, PropertyResponseContent<V>> responseConverter) : AbsProperty<K, V, ConverterPropertyModel, IPropertyExtension> where K : IRequestKey where A : IRequestKey
{
    public override ConverterPropertyModel CreateModel()
    {
        return new ConverterPropertyModel();
    }

    public override LockResource GetLockResource(K key, LockType type)
    {
        return source.GetLockResource(keyConverter(key), type);
    }

    public override void RearrangeRequests(PropertyContext<K, V, ConverterPropertyModel, IPropertyExtension> context)
    {
        ConverterPropertyModel model = context.Model;
        foreach (SealedPropertyRequest<K, V> serverRequest in context.NewRequests)
        {
            A convertedKey;
            try
            {
                convertedKey = keyConverter(serverRequest.RequestContent.Key);
            }
            catch (Exception e)
            {
                Logger.AssertNotReachHere("DC4669BD138CDCBB", e);
                context.Respond(serverRequest.Id, PropertyResponseContent<V>.NewFailedResponse());
                continue;
            }
            B? convertedValue;
            try
            {
                convertedValue = valueConverter(serverRequest.RequestContent.Value);
            }
            catch (Exception e)
            {
                Logger.AssertNotReachHere("A2FEA00738EB0F32", e);
                context.Respond(serverRequest.Id, PropertyResponseContent<V>.NewFailedResponse());
                continue;
            }
            PropertyRequestContent<A, B> convertedRequest = serverRequest.RequestContent.WithKeyAndValue(convertedKey, convertedValue);
            OperationResult result = context.Request(source, convertedRequest, OnResponse, out long requestId);
            if (result != OperationResult.Successful)
            {
                context.Respond(serverRequest.Id, PropertyResponseContent<V>.NewFailedResponse());
                continue;
            }
            model.requests[requestId] = serverRequest.Id;
        }
    }

    public override void ProcessRequests(PropertyContext<K, V, ConverterPropertyModel, IPropertyExtension> context, IProcessCallback callback)
    {
        callback.PostOnServerThread(true, null);
    }

    private void OnResponse(PropertyContext<K, V, ConverterPropertyModel, IPropertyExtension> context, long id, PropertyResponseContent<B> response)
    {
        if (!context.Model.requests.Remove(id, out long originId))
        {
            Logger.AssertNotReachHere("");
            return;
        }
        PropertyResponseContent<V> convertedResponse;
        try
        {
            convertedResponse = responseConverter(response);
        }
        catch (Exception e)
        {
            Logger.AssertNotReachHere("F7A6288C8CB7584C", e);
            convertedResponse = PropertyResponseContent<V>.NewFailedResponse();
        }
        context.Respond(originId, convertedResponse);
    }
}
