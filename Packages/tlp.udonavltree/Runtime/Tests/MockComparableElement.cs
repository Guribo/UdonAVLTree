#if !COMPILER_UDONSHARP && UNITY_EDITOR

using JetBrains.Annotations;
using TLP.UdonUtils.Runtime;
using TLP.UdonUtils.Runtime.Common;
using UnityEngine;

namespace TLP.UdonAVLTree.Tests.Runtime
{
    [DefaultExecutionOrder(ExecutionOrder)]
    [TlpDefaultExecutionOrder(typeof(MockComparableElement), ExecutionOrder)]
    public class MockComparableElement : TlpBaseBehaviour
    {
        #region ExecutionOrder
        public override int ExecutionOrderReadOnly => ExecutionOrder;

        [PublicAPI]
        public new const int ExecutionOrder = TlpExecutionOrder.TimeSourcesStart + 1;
        #endregion

        public int valueToCompare;

        public override string ToString() {
            return valueToCompare.ToString();
        }
    }
}

#endif