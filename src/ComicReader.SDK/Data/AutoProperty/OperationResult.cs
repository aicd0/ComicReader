// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

namespace ComicReader.SDK.Data.AutoProperty;

public enum OperationResult
{
    Successful = 0,
    PropertyError,
    OperateOnNonServerThread,
    RecursiveRequest,
    ReachMaxRequests,
    GetLockResourceFail,
    AcquireLockFail,
    InvalidArgument,
    NoPermission,
    ExceptionInUserCode,
    UnhandledRequest,
}
