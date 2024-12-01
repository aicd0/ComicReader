// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

namespace ComicReader.Common.Flow;

internal class BooleanInputFlow : BooleanFlow
{
    readonly InputOperation<bool> _operation;

    private BooleanInputFlow(FlowScope scope, InputOperation<bool> operation) : base(scope, operation)
    {
        _operation = operation;
    }

    public void Set(bool input, bool isIntermediate = false)
    {
        _operation.Set(input, isIntermediate);
    }

    public static BooleanInputFlow New(bool initial)
    {
        return New(initial, FlowScope.GlobalScope);
    }

    public static BooleanInputFlow New(bool initial, FlowScope scope)
    {
        return new BooleanInputFlow(scope, new InputOperation<bool>(initial, scope));
    }
}
