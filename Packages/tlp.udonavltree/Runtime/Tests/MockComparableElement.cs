#if !COMPILER_UDONSHARP && UNITY_EDITOR

using TLP.UdonUtils.Runtime;

namespace TLP.UdonAVLTree.Tests.Runtime
{
    public class MockComparableElement : TlpBaseBehaviour
    {
        public int valueToCompare;

        public override string ToString()
        {
            return valueToCompare.ToString();
        }
    }
}

#endif