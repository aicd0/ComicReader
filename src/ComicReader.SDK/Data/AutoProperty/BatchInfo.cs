// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using ComicReader.SDK.Common.DebugTools;

namespace ComicReader.SDK.Data.AutoProperty;

internal class BatchInfo
{
    public ExternalBatchRequest Request { get; }
    public ExternalBatchResponse Response { get; } = new();
    public TaskCompletionSource<ExternalBatchResponse> CompletionSource { get; } = new TaskCompletionSource<ExternalBatchResponse>();
    public int RemainingRequests { get; set; }

    public BatchInfo(ExternalBatchRequest request)
    {
        Request = request;
    }

    public void CompleteOne()
    {
        Logger.Assert(RemainingRequests > 0, "A5E3AF05B00F7CED");
        if (--RemainingRequests == 0)
        {
            CompleteNow();
        }
    }

    public void CompleteNow()
    {
        CompletionSource.TrySetResult(Response);
    }
}
