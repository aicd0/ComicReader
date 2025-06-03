// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using ComicReader.SDK.Common.DebugTools;

namespace ComicReader.SDK.Data.AutoProperty.Presets;

internal class SplitProperty<T>(IQRProperty<T, T> readSource, IQRProperty<T, T> modifySource) : AbsProperty<T, T, VoidType, IPropertyExtension>
{
    public override VoidType CreateModel()
    {
        return VoidType.Instance;
    }

    public override List<IProperty> GetDependentProperties()
    {
        return [readSource];
    }

    public override void RearrangeRequests(PropertyContext<T, T, VoidType, IPropertyExtension> context)
    {
        foreach (SealedPropertyRequest<T> serverRequest in context.NewRequests)
        {
            PropertyRequestContent<T> request = serverRequest.RequestContent;
            switch (request.Type)
            {
                case RequestType.Read:
                    context.Redirect(serverRequest.Id, readSource);
                    break;
                case RequestType.Modify:
                    context.Redirect(serverRequest.Id, modifySource);
                    break;
                default:
                    Logger.AssertNotReachHere("28569D6FECE370F0");
                    continue;
            }
        }
    }

    public override void ProcessRequests(PropertyContext<T, T, VoidType, IPropertyExtension> context, IProcessCallback callback)
    {
        callback.PostCompletion(null);
    }
}
