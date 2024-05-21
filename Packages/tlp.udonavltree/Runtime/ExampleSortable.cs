using JetBrains.Annotations;
using TLP.UdonUtils;
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

namespace TLP.UdonAVLTree.Runtime
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class ExampleSortable : TlpBaseBehaviour
    {
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
