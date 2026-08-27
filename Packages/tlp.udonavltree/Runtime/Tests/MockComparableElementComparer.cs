#if !COMPILER_UDONSHARP && UNITY_EDITOR

using JetBrains.Annotations;
using TLP.UdonUtils.Runtime.Common;
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

namespace TLP.UdonAVLTree.Tests.Runtime
{
    [DefaultExecutionOrder(ExecutionOrder)]
    [TlpDefaultExecutionOrder(typeof(MockComparableElementComparer), ExecutionOrder)]
    public class MockComparableElementComparer : Comparer
    {
        #region ExecutionOrder
        public override int ExecutionOrderReadOnly => ExecutionOrder;

        [PublicAPI]
        public new const int ExecutionOrder = Comparer.ExecutionOrder + 1;
        #endregion

        protected override bool _ComparisonImplementation(
                UdonSharpBehaviour first,
                UdonSharpBehaviour second,
                out int comparisonResult
        ) {
#if TLP_DEBUG
            _DebugLog(nameof(MockComparableElementComparer));
#endif
            comparisonResult = 0;
            if (!Utilities.IsValid(first)) {
                return false;
            }

            if (!Utilities.IsValid(second)) {
                return false;
            }

            var firstMock = (MockComparableElement)first;
            var secondMock = (MockComparableElement)second;

            if (!Utilities.IsValid(firstMock)) {
                return false;
            }

            if (!Utilities.IsValid(secondMock)) {
                return false;
            }

            comparisonResult = firstMock.ValueToCompare.CompareTo(secondMock.ValueToCompare);
            return true;
        }
    }
}

#endif