// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System;

namespace ComicReader.Common.Flow;

internal class BooleanFlow : OutputFlow<bool>
{
    protected BooleanFlow(FlowScope scope, IFlowOperation<bool> operation) : base(scope, operation)
    {
    }

    public static BooleanFlow operator !(BooleanFlow c1)
    {
        return new BooleanFlow(c1._scope, new NegationOperation(c1));
    }

    private class NegationOperation(BooleanFlow c1) : IFlowOperation<bool>
    {
        public void RegisterObserver(Action observer)
        {
            c1.ObserveIntermediate(observer);
        }

        public bool CalculateValue()
        {
            return !c1.Get();
        }

        public bool CalculateIntermediateValue()
        {
            return !c1.GetIntermediate();
        }
    }
}
