using TLP.UdonUtils.Common;
using UdonSharp;
using VRC.SDKBase;

namespace TLP.UdonAVLTree.Runtime
{
    public class ExampleComparer : Comparer
    {
        protected override bool ComparisonImplementation(
            UdonSharpBehaviour first,
            UdonSharpBehaviour second,
            out int comparisonResult
        )
        {
#if TLP_DEBUG
            DebugLog(nameof(ExampleComparer));
#endif
            comparisonResult = 0;
            if (!Utilities.IsValid(first))
            {
                return false;
            }

            if (!Utilities.IsValid(second))
            {
                return false;
            }

            var firstMock = (ExampleSortable)first;
            var secondMock = (ExampleSortable)second;

            if (!Utilities.IsValid(firstMock))
            {
                return false;
            }

            if (!Utilities.IsValid(secondMock))
            {
                return false;
            }

            comparisonResult = firstMock.value.CompareTo(secondMock.value);
            return true;
        }
    }
}