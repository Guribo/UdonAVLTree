using JetBrains.Annotations;
using TLP.UdonUtils.Runtime.Common;
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

namespace TLP.UdonAVLTree.Runtime
{
    [DefaultExecutionOrder(ExecutionOrder)]
    [TlpDefaultExecutionOrder(typeof(ExampleComparer), ExecutionOrder)]
    public class ExampleComparer : Comparer
    {
        #region ExecutionOrder
        public override int ExecutionOrderReadOnly => ExecutionOrder;

        [PublicAPI]
        public new const int ExecutionOrder = Comparer.ExecutionOrder + 2;
        #endregion


        protected override bool ComparisonImplementation(
                UdonSharpBehaviour first,
                UdonSharpBehaviour second,
                out int comparisonResult
        ) {
#if TLP_DEBUG
            DebugLog(nameof(ExampleComparer));
#endif
            comparisonResult = 0;
            if (!Utilities.IsValid(first)) {
                return false;
            }

            if (!Utilities.IsValid(second)) {
                return false;
            }

            var a = (ExampleSortable)first;
            var b = (ExampleSortable)second;

            if (!Utilities.IsValid(a)) {
                return false;
            }

            if (!Utilities.IsValid(b)) {
                return false;
            }

            comparisonResult = a.value.CompareTo(b.value);
            return true;
        }
    }
}