using JetBrains.Annotations;
using TLP.UdonUtils.Runtime;
using TLP.UdonUtils.Runtime.Common;
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

namespace TLP.UdonAVLTree.Runtime
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    [DefaultExecutionOrder(ExecutionOrder)]
    [TlpDefaultExecutionOrder(typeof(ExampleSortable), ExecutionOrder)]
    public class ExampleSortable : TlpBaseBehaviour
    {
        #region ExecutionOrder
        public override int ExecutionOrderReadOnly => ExecutionOrder;

        [PublicAPI]
        public new const int ExecutionOrder = TlpExecutionOrder.TimeSourcesStart + 2;
        #endregion

        public int value;

        #region Comparer Interface

        [HideInInspector, PublicAPI]
        public UdonSharpBehaviour toCompare;
        [HideInInspector, PublicAPI]
        public bool compareSuccess;
        [HideInInspector, PublicAPI]
        public int compareResult;
        
        [PublicAPI]
        public virtual void CompareValues()
        {
            if (!Utilities.IsValid(toCompare))
            {
                compareSuccess = false;
                return;
            }

            var other = (ExampleSortable)toCompare;
            if (!Utilities.IsValid(other))
            {
                compareSuccess = false;
                return;
            }

            compareResult = value.CompareTo(other.value);
            compareSuccess = true;
        }
        #endregion

        public override string ToString()
        {
            return value.ToString();
        }
    }
}
