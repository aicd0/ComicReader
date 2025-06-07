// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using ComicReader.SDK.Common.DebugTools;

namespace ComicReader.SDK.Data.AutoProperty.Presets;

public class SplitProperty<K, V>(IKVProperty<K, V> readSource, IKVProperty<K, V> writeSource) : AbsProperty<K, V, VoidType, IPropertyExtension> where K : IRequestKey
{
    public override VoidType CreateModel()
    {
        return VoidType.Instance;
    }

    public override LockResource GetLockResource(K key, LockType type)
    {
        return type switch
        {
            LockType.Read => readSource.GetLockResource(key, LockType.Read),
            LockType.Write => writeSource.GetLockResource(key, LockType.Write),
            _ => throw new ArgumentOutOfRangeException(nameof(type), "Invalid lock type for SplitProperty.")
        };
    }

    public override void RearrangeRequests(PropertyContext<K, V, VoidType, IPropertyExtension> context)
    {
        foreach (SealedPropertyRequest<K, V> serverRequest in context.NewRequests)
        {
            PropertyRequestContent<K, V> request = serverRequest.RequestContent;
            switch (request.Type)
            {
                case RequestType.Read:
                    context.Redirect(serverRequest.Id, readSource);
                    break;
                case RequestType.Modify:
                    context.Redirect(serverRequest.Id, writeSource);
                    break;
                default:
                    Logger.AssertNotReachHere("28569D6FECE370F0");
                    continue;
            }
        }
    }

    public override void ProcessRequests(PropertyContext<K, V, VoidType, IPropertyExtension> context, IProcessCallback callback)
    {
        callback.PostCompletion(null);
    }
}
