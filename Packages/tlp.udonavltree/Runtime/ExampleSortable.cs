using JetBrains.Annotations;
using TLP.UdonUtils.Runtime;
using UdonSharp;
using UnityEngine;
using UnityEngine.Serialization;
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

        [FormerlySerializedAs("value")] public int Value;

        #region Comparer Interface

        [FormerlySerializedAs("toCompare")]
        [HideInInspector, PublicAPI]
        public UdonSharpBehaviour ToCompare;
        [FormerlySerializedAs("compareSuccess")]
        [HideInInspector, PublicAPI]
        public bool CompareSuccess;
        [FormerlySerializedAs("compareResult")]
        [HideInInspector, PublicAPI]
        public int CompareResult;
        
        [PublicAPI]
        public virtual void _CompareValues()
        {
            if (!Utilities.IsValid(ToCompare))
            {
                CompareSuccess = false;
                return;
            }

            var other = (ExampleSortable)ToCompare;
            if (!Utilities.IsValid(other))
            {
                CompareSuccess = false;
                return;
            }

            CompareResult = Value.CompareTo(other.Value);
            CompareSuccess = true;
        }
        #endregion

        public override string ToString()
        {
            return Value.ToString();
        }
    }
}
