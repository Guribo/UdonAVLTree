using System.Collections.Generic;
using System.Text;
using JetBrains.Annotations;
using TLP.UdonUtils.Runtime;
using TLP.UdonUtils.Runtime.Common;
using TLP.UdonUtils.Runtime.Extensions;
using TLP.UdonUtils.Runtime.Player;
using TLP.UdonUtils.Runtime.Pool;
using UdonSharp;
using UnityEngine;
using UnityEngine.Serialization;
using VRC.SDK3.Data;
using VRC.SDKBase;

namespace TLP.UdonAVLTree.Runtime
{
    /// <summary>
    /// Loosely based on the AVL tree implementation by KadirEmreOto,
    /// but converted to UDON and extended with using empty child
    /// references as wires for faster in-order access of nodes.
    /// <seealso cref="https://github.com/KadirEmreOto/AVL-Tree"/>
    /// </summary>
    // ReSharper disable once InconsistentNaming
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    [DefaultExecutionOrder(ExecutionOrder)]
    [TlpDefaultExecutionOrder(typeof(AvlTree), ExecutionOrder)]
    public class AvlTree : TlpBaseBehaviour
    {
        public override int ExecutionOrderReadOnly => ExecutionOrder;

        [PublicAPI]
        public new const int ExecutionOrder = PlayerBlackList.ExecutionOrder + 1;


        public int Size { get; internal set; }

        [FormerlySerializedAs("comparer")]
        public Comparer Comparer;

        internal DataList RootNode;
        internal readonly DataList NodePool = new DataList();

        protected override bool SetupAndValidate() {
            if (!base.SetupAndValidate()) {
                return false;
            }

            if (!Utilities.IsValid(Comparer)) {
                Error($"{nameof(Comparer)} not set");
                return false;
            }

            return true;
        }

        public bool Add(TlpBaseBehaviour newElement) {
#if TLP_DEBUG
            DebugLog(nameof(Add));
#endif

            if (!HasStartedOk) {
                Error($"{nameof(Add)}: Not initialized");
                return false;
            }

            var newNode = AvlTreeNodeUtils.CreateNode(NodePool);
            newNode.SetPayload(newElement);

            var current = RootNode;

            while (Utilities.IsValid(current)) {
                // ReSharper disable once InlineOutVariableDeclaration not supported yet by Udon
                int comparisonResult;
                bool comparisonSuccess = Comparer.Compare(newElement, current.GetPayload(), out comparisonResult);
                if (!comparisonSuccess) {
                    Error("Add failed on comparison");
                    newNode.ReturnToPool(NodePool);
                    return false;
                }

                if (comparisonResult == -1) {
                    if (!current.IsLeftValidNode()) {
                        // set parent
                        newNode.SetParent(current);

                        // take of parents left wire
                        newNode.SetLeft(current.GetLeft());
                        newNode.SetLeftIsWire(current.LeftIsWire());

                        // connect the right wire to the parent
                        newNode.SetRight(current);
                        newNode.SetRightIsWire(true);

                        // attach to the parents left side
                        current.SetLeft(newNode);
                        current.SetLeftIsWire(false);

                        // leave for balancing
                        current = newNode;
                        break;
                    }

                    current = current.GetLeft();
                } else {
                    if (!current.IsRightValidNode()) {
                        // set parent
                        newNode.SetParent(current);

                        // take of parents right wire
                        newNode.SetRight(current.GetRight());
                        newNode.SetRightIsWire(current.RightIsWire());

                        // connect the left wire to the parent
                        newNode.SetLeft(current);
                        newNode.SetLeftIsWire(true);

                        // attach to the parents right side
                        current.SetRight(newNode);
                        current.SetRightIsWire(false);

                        // leave for balancing
                        current = newNode;
                        break;
                    }

                    current = current.GetRight();
                }
            }

            if (!Utilities.IsValid(current)) {
                current = newNode;
            }

            Balance(current);
            Size++;

            // if (!VerifyChildrenConnections())
            // {
            //     Error("Children broke");
            //     return false;
            // }
            //
            // if (!VerifyParentConnections())
            // {
            //     Error("Parents broke");
            //     return false;
            // }

            return true;
        }

        public bool Remove(UdonSharpBehaviour elementToRemove) {
            DebugLog(nameof(Remove));
            if (!Utilities.IsValid(elementToRemove)) {
                Error(nameof(elementToRemove));
                return false;
            }

            var nodeToDelete = FindNode(elementToRemove, RootNode);

            if (!Utilities.IsValid(nodeToDelete)) {
                // the value does not exist in tree
                Warn($"Value {elementToRemove.ToString()} does not exist");
                return false;
            }

            bool leftValid = nodeToDelete.IsLeftValidNode();
            bool rightValid = nodeToDelete.IsRightValidNode();

            DataList balanceStart = null;
            if (!rightValid && !leftValid) {
                balanceStart = RemoveLeafNode(nodeToDelete);
            } else if (nodeToDelete.GetBalance() < 0) {
                balanceStart = RemoveRootNodeOfLeftHeavyTree(nodeToDelete, false);
            } else {
                balanceStart = RemoveRootNodeOfLeftHeavyTree(nodeToDelete, true);
            }

            if (Utilities.IsValid(balanceStart)) {
                Balance(balanceStart);
            } else {
                RootNode = null;
            }

            Size--;

            return true;
        }

#if !COMPILER_UDONSHARP && UNITY_EDITOR

        /*
         #region Debugging

        public bool VerifyParentConnections()
        {
            var parents = new Dictionary<AVLTreeNode, HashSet<AVLTreeNode>>();
            var avlTreeNodes = TreeNodes.GetComponentsInChildren<AVLTreeNode>(true);
            var nullParents = new HashSet<AVLTreeNode>();
            foreach (var avlTreeNode in avlTreeNodes)
            {
                parents.Add(avlTreeNode, new HashSet<AVLTreeNode>());
            }

            foreach (var node in avlTreeNodes)
            {
                if (node.IsLeftValidNode())
                {
                    Debug.Assert(ReferenceEquals(node.left.parent, node), "node.left.parent == this");
                    if (!ReferenceEquals(node.left.parent, node))
                    {
                        return false;
                    }
                }

                if (node.IsRightValidNode())
                {
                    Debug.Assert(ReferenceEquals(node.right.parent, node), "node.right.parent == this");
                    if (!ReferenceEquals(node.right.parent, node))
                    {
                        return false;
                    }
                }

                if (node.parent)
                {
                    parents[node.parent].Add(node);
                }
                else
                {
                    nullParents.Add(node);
                }
            }

            bool failed = false;

            if (nullParents.Count != 1)
            {
                foreach (var avlTreeNode in nullParents)
                {
                    Error("Has null parent: " + avlTreeNode.PayloadToString());
                    failed = true;
                }
            }

            foreach (var keyValuePair in parents)
            {
                if (keyValuePair.Value.Count > 2)
                {
                    foreach (var child in keyValuePair.Value)
                    {
                        Error(
                            keyValuePair.Key.PayloadToString() + ": refernced by (parent ref): " +
                            child.PayloadToString()
                        );
                        failed = true;
                    }
                }
            }

            return !failed;
        }

        public bool VerifyChildrenConnections() {
            var allNodes = TreeNodes.GetComponentsInChildren<AVLTreeNode>(true);
            var leftChildren = new HashSet<AVLTreeNode>();
            var rightChildren = new HashSet<AVLTreeNode>();

            foreach (var avlTreeNode in allNodes) {
                if (!FindNode(avlTreeNode.payload, avlTreeNode)) {
                    return false;
                }

                if (avlTreeNode.IsLeftValidNode()) {
                    if (leftChildren.Contains(avlTreeNode.left)) {
                        Error(avlTreeNode.PayloadToString() + " also links to " + avlTreeNode.GetLeft().PayloadToString());
                        return false;
                    }

                    leftChildren.Add(avlTreeNode.left);
                }

                if (avlTreeNode.IsRightValidNode()) {
                    if (rightChildren.Contains(avlTreeNode.right)) {
                        Error(avlTreeNode.PayloadToString() + " also links to " + avlTreeNode.GetLeft().PayloadToString());
                        return false;
                    }

                    rightChildren.Add(avlTreeNode.right);
                }
            }

            return true;
        }

        internal bool VerifyBalance() {
            bool success = true;
            foreach (var node in TreeNodes.GetComponentsInChildren<AVLTreeNode>(true)) {
                int balance = node.Balance;
                if (balance < -1 || balance > 1) {
                    Error(node.PayloadToString() + " has wrong balance " + balance);
                    success = false;
                }
            }

            return success;
        }


        private void LogList(string prefix, List<AVLTreeNode> list) {
            var sb = new StringBuilder();
            sb.Append(prefix).Append("\n{");
            foreach (var i in list) {
                sb.Append(i.PayloadToString()).Append(",");
            }


            sb.Replace(",", "", sb.Length - 1, 1);
            sb.Append("}");

            Debug.Log(sb.ToString());
        }
        #endregion
        */

#endif

        [PublicAPI]
        public UdonSharpBehaviour Contains(UdonSharpBehaviour searchElement) {
            DebugLog(nameof(Contains));
            var avlTreeNode = FindNode(searchElement, RootNode);
            if (Utilities.IsValid(avlTreeNode)) {
                return avlTreeNode.GetPayload();
            }

            return null;
        }


        internal DataList FindNode(UdonSharpBehaviour searchElement, DataList start) {
            if (!Utilities.IsValid(Comparer)) {
                Error($"{nameof(Comparer)} invalid");
                return null;
            }

            while (true) {
                if (!Utilities.IsValid(start)) {
                    // node not found, first exit point
                    return null;
                }

                // ReSharper disable once InlineOutVariableDeclaration not supported yet by Udon
                int comparisonResult;

                // ReSharper disable once PossibleNullReferenceException False positive, see Utilities.IsValid(start)
                bool comparisonSuccess = Comparer.Compare(searchElement, start.GetPayload(), out comparisonResult);
                if (!comparisonSuccess) {
                    Error($"Comparison failed when looking for Node '{searchElement.GetScriptPathInScene()}' using comparer '{Comparer.GetScriptPathInScene()}'");

                    // second exit point
                    return null;
                }

                if (comparisonResult == 0) {
                    // entry found, third exit point
                    return start;
                }

                if (comparisonResult < 0) {
                    start = start.IsLeftValidNode() ? start.GetLeft() : null;
                } else {
                    start = start.IsRightValidNode() ? start.GetRight() : null;
                }
            }
        }


        private DataList RemoveLeafNode(DataList nodeToDelete) {
            var successor = nodeToDelete.GetParent();
            if (Utilities.IsValid(successor)) {
                if (ReferenceEquals(successor.GetLeft(), nodeToDelete)) {
                    successor.SetLeft(nodeToDelete.GetLeft());
                    successor.SetLeftIsWire(nodeToDelete.LeftIsWire());
                } else {
                    successor.SetRight(nodeToDelete.GetRight());
                    successor.SetRightIsWire(nodeToDelete.RightIsWire());
                }
            } else {
                RootNode = null;
            }

            nodeToDelete.ReturnToPool(NodePool);
            return successor;
        }

        internal DataList RemoveRootNodeOfRightHeavyTree(DataList toRemove) {
            var successor = AvlTreeNodeUtils.GetFirst(toRemove.GetRight());

            DataList balancingStart;

            bool successorHasLeftChildren = !ReferenceEquals(successor, toRemove.GetRight());
            if (successorHasLeftChildren) {
                balancingStart = successor.GetParent();

                if (successor.IsRightValidNode()) {
                    // attach child of successor to parent of successor
                    var childOfSuccessor = successor.GetRight();

                    if (!ReferenceEquals(balancingStart, childOfSuccessor)) {
                        balancingStart.SetLeft(childOfSuccessor);
                        childOfSuccessor.SetParent(balancingStart);
                    }
                } else {
                    // connect the right tree wire to the new root
                    balancingStart.SetLeft(successor);
                    balancingStart.SetLeftIsWire(true);
                }

                successor.SetRight(toRemove.GetRight());
                successor.SetRightIsWire(false);

                successor.SetParent(toRemove.GetParent());

                toRemove.GetLeft().SetParent(successor);
                successor.SetLeft(toRemove.GetLeft());
                successor.SetLeftIsWire(false);

                toRemove.GetRight().SetParent(successor);
            } else {
                balancingStart = successor;

                successor.SetParent(toRemove.GetParent());

                toRemove.GetLeft().SetParent(successor);
                successor.SetLeft(toRemove.GetLeft());
                successor.SetLeftIsWire(false);
            }

            // connect the right trees wire to the new root
            AvlTreeNodeUtils.GetLast(successor.GetLeft()).SetRight(successor);
            AvlTreeNodeUtils.GetLast(successor.GetLeft()).SetRightIsWire(true);

            if (Utilities.IsValid(toRemove.GetParent())) {
                if (ReferenceEquals(toRemove.GetParent().GetLeft(), toRemove)) {
                    toRemove.GetParent().SetLeft(successor);

                    var mostRight = AvlTreeNodeUtils.GetLast(successor);
                    mostRight.SetRight(toRemove.GetParent());
                    mostRight.SetRightIsWire(true);
                } else {
                    toRemove.GetParent().SetRight(successor);
                    var mostLeft = AvlTreeNodeUtils.GetFirst(successor);
                    mostLeft.SetLeft(toRemove.GetParent());
                    mostLeft.SetLeftIsWire(true);
                }
            } else {
                var mostLeft = AvlTreeNodeUtils.GetFirst(successor);
                mostLeft.SetLeft(null);
                mostLeft.SetLeftIsWire(false);

                var mostRight = AvlTreeNodeUtils.GetLast(successor);
                mostRight.SetRight(null);
                mostRight.SetRightIsWire(false);
            }

            RootNode = balancingStart;
            while (Utilities.IsValid(RootNode.GetParent())) {
                RootNode = RootNode.GetParent();
            }

            toRemove.ReturnToPool(NodePool);
            return balancingStart;
        }

        /// <summary>
        /// given a node that is the root of a (sub-) tree it replaces it with the
        /// single left child. Must only have a single node in the left side!
        /// </summary>
        /// <param name="root"></param>
        /// <returns></returns>
        internal DataList ReplaceRootWithLeftChild(DataList root) {
            if (root.IsRightValidNode()) {
                var minRight = AvlTreeNodeUtils.GetFirst(root.GetRight());

                root.GetRight().SetParent(root.GetLeft());

                minRight.SetLeft(root.GetLeft());
                minRight.SetLeftIsWire(true);
            }

            root.GetLeft().SetParent(root.GetParent());


            root.GetLeft().SetRight(root.GetRight());
            root.GetLeft().SetRightIsWire(root.RightIsWire());

            if (ReferenceEquals(root, RootNode)) {
                RootNode = root.GetLeft();
            } else {
                if (ReferenceEquals(root.GetParent().GetLeft(), root)) {
                    root.GetParent().SetLeft(root.GetLeft());
                } else {
                    root.GetParent().SetRight(root.GetLeft());
                }
            }

            return root.GetLeft();
        }


        internal DataList ReplaceRootWithRightChild(DataList root) {
            if (root.IsLeftValidNode()) {
                var maxLeft = AvlTreeNodeUtils.GetLast(root.GetLeft());

                root.GetLeft().SetParent(root.GetRight());

                maxLeft.SetRight(root.GetRight());
                maxLeft.SetRightIsWire(true);
            }

            root.GetRight().SetParent(root.GetParent());

            root.GetRight().SetLeft(root.GetLeft());
            root.GetRight().SetLeftIsWire(root.LeftIsWire());

            if (ReferenceEquals(root, RootNode)) {
                RootNode = root.GetRight();
            } else {
                if (ReferenceEquals(root.GetParent().GetRight(), root)) {
                    root.GetParent().SetRight(root.GetRight());
                } else {
                    root.GetParent().SetLeft(root.GetRight());
                }
            }

            return root.GetRight();
        }

        /// <summary>
        /// B -> C
        ///     C
        ///   B   (?)
        /// A    (?)
        /// </summary>
        /// <param name="root"></param>
        /// <returns></returns>
        internal DataList ReplaceRootWithLeftTreeLeftHeavy(DataList root) {
            var maxLeftTree = AvlTreeNodeUtils.GetLast(root.GetLeft());
            var minRightTree = AvlTreeNodeUtils.GetFirst(root.GetRight());

            if (maxLeftTree.IsLeftValidNode()) {
                maxLeftTree.RotateRight();
            }

            var balancingStartNode = maxLeftTree.GetParent();

            maxLeftTree.SetParent(root.GetParent());

            maxLeftTree.SetLeft(root.GetLeft());
            maxLeftTree.SetLeftIsWire(false);


            maxLeftTree.SetRight(root.GetRight());
            maxLeftTree.SetRightIsWire(root.RightIsWire());

            minRightTree.SetLeft(maxLeftTree);

            root.GetRight().SetParent(maxLeftTree);

            if (ReferenceEquals(balancingStartNode, root.GetLeft())) {
                balancingStartNode.SetParent(maxLeftTree);
            }

            balancingStartNode.SetRightIsWire(true);
            ;


            root.GetLeft().SetParent(maxLeftTree);

            if (Utilities.IsValid(root.GetParent())) {
                if (ReferenceEquals(root.GetParent().GetLeft(), root)) {
                    root.GetParent().SetLeft(maxLeftTree);
                } else {
                    root.GetParent().SetRight(maxLeftTree);
                }
            }

            return balancingStartNode;
        }


        internal DataList ReplaceRootWithRightTreeRightHeavy(DataList root) {
            var minRightTree = AvlTreeNodeUtils.GetFirst(root.GetRight());
            var maxLeftTree = AvlTreeNodeUtils.GetLast(root.GetLeft());

            if (minRightTree.IsRightValidNode()) {
                minRightTree.RotateLeft();
            }

            var balancingStartNode = minRightTree.GetParent();

            minRightTree.SetParent(root.GetParent());

            minRightTree.SetRight(root.GetRight());
            minRightTree.SetRightIsWire(false);


            minRightTree.SetLeft(root.GetLeft());
            minRightTree.SetLeftIsWire(root.LeftIsWire());

            maxLeftTree.SetRight(minRightTree);

            root.GetLeft().SetParent(minRightTree);

            if (ReferenceEquals(balancingStartNode, root.GetRight())) {
                balancingStartNode.SetParent(minRightTree);
            }

            balancingStartNode.SetLeftIsWire(true);


            root.GetRight().SetParent(minRightTree);

            if (Utilities.IsValid(root.GetParent())) {
                if (ReferenceEquals(root.GetParent().GetRight(), root)) {
                    root.GetParent().SetRight(minRightTree);
                } else {
                    root.GetParent().SetLeft(minRightTree);
                }
            }

            return balancingStartNode;
        }


        internal DataList RemoveRootNodeOfLeftHeavyTree(DataList toRemove, bool isLeft) {
            var successor = isLeft
                    ? AvlTreeNodeUtils.GetLast(toRemove.GetLeft())
                    : AvlTreeNodeUtils.GetFirst(toRemove.GetRight());

            // left/right child has no right/left child
            if (isLeft
                        ? ReferenceEquals(successor, toRemove.GetLeft())
                        : ReferenceEquals(successor, toRemove.GetRight())) {
                successor = isLeft
                        ? ReplaceRootWithLeftChild(toRemove)
                        : ReplaceRootWithRightChild(toRemove);
            } else {
                successor = isLeft
                        ? ReplaceRootWithLeftTreeLeftHeavy(toRemove)
                        : ReplaceRootWithRightTreeRightHeavy(toRemove);
            }

            toRemove.ReturnToPool(NodePool);
            return successor;
        }

#if !COMPILER_UDONSHARP && UNITY_EDITOR

        private bool CheckForCyclicParentRelations(DataList tmp) {
            var visited = new HashSet<DataList>();
            while (Utilities.IsValid(tmp)) {
                if (!visited.Add(tmp)) {
                    Error(tmp.PayloadToString() + "Already visited");
                    return true;
                }

                tmp = tmp.GetParent();
            }

            return false;
        }
#endif


        private void Balance(DataList begin) {
            for (var current = begin; Utilities.IsValid(current); current = current.GetParent()) {
                RootNode = current;
                current.UpdateValues();

                if (current.GetBalance() >= 2 && current.GetLeft().GetBalance() >= 0) // left - left
                {
                    current = current.RotateRight();
                    RootNode = current;
                } else if (current.GetBalance() >= 2) {
                    // left - right
                    current.SetLeft(current.GetLeft().RotateLeft());
                    current = current.RotateRight();
                    RootNode = current;
                } else if (current.GetBalance() <= -2 && current.GetRight().GetBalance() <= 0) // right - right
                {
                    current = current.RotateLeft();
                    RootNode = current;
                } else if (current.GetBalance() <= -2) {
                    // right - left
                    current.SetRight(current.GetRight().RotateRight());
                    current = current.RotateLeft();
                    RootNode = current;
                }
            }

            if (!Utilities.IsValid(RootNode)) {
                Info("Tree is empty, nothing to balance");
            }
        }

        public override string ToString() {
            return Utilities.IsValid(RootNode) ? RootNode.ToStringWithChildren() : "Empty";
        }

        public StringBuilder Display(DataList cur, int depth = 0, int state = 0, StringBuilder sb = null) {
            if (sb == null) {
                sb = new StringBuilder();
            }

            if (!Utilities.IsValid(cur)) {
                Warn("Invalid node");
                return sb;
            }

            // state: 1 -> left, 2 -> right , 0 -> root
            if (cur.IsLeftValidNode()) {
                sb = Display(cur.GetLeft(), depth + 1, 1, sb);
            }

            int count = 0;
            for (int i = 0; i < depth; i++) {
                if (count++ > 1000) {
                    Error("Balance: Potential endless loop detected");
                    return sb;
                }

                sb.Append("|    ");
            }

            if (state == 1) // left
            {
                sb.Append("┌───");
            } else if (state == 2) // right
            {
                sb.Append("└───");
            }

            sb.Append("[")
                    .Append(cur.PayloadToString())
                    .Append("](")
                    .Append(cur.GetNodeCount().ToString())
                    .Append(", ")
                    .Append(cur.GetTreeHeight())
                    .Append(", ")
                    .Append(cur.GetBalance())
                    .Append(")");


            if (!cur.IsLeftValidNode()) {
                sb.Append(" lW=" + (Utilities.IsValid(cur.GetLeft()) ? cur.GetLeft().PayloadToString() : "null"));
            }

            if (!cur.IsRightValidNode()) {
                sb.Append(" rW=" + (Utilities.IsValid(cur.GetRight()) ? cur.GetRight().PayloadToString() : "null"));
            }

            sb.Append("\n");

            if (cur.IsRightValidNode()) {
                sb = Display(cur.GetRight(), depth + 1, 2, sb);
            }

            return sb;
        }

        private int m_LastFrame;
        private int m_GetCallCount;

        public TlpBaseBehaviour Get(int index) {
#if TLP_DEBUG
            DebugLog(nameof(Get));
#endif
            int frame = Time.renderedFrameCount;
            if (frame != m_LastFrame) {
                m_LastFrame = frame;
                if (m_GetCallCount > 0) {
                    DebugLog($"Get was called {m_GetCallCount} times");
                    m_GetCallCount = 0;
                }
            }

            ++m_GetCallCount;


            if (index < 0 || index >= Size) {
                return null;
            }

            var current = RootNode;
            int left = current.IsLeftValidNode() ? current.GetLeft().GetNodeCount() : 0;

            while (left != index) {
                if (left < index) {
                    index -= left + 1;

                    current = current.GetRight();
                    left = current.IsLeftValidNode() ? current.GetLeft().GetNodeCount() : 0;
                } else {
                    current = current.GetLeft();
                    left = current.IsLeftValidNode() ? current.GetLeft().GetNodeCount() : 0;
                }
            }

            return current.GetPayload();
        }

        public bool IsEmpty() {
            return Size < 1;
        }

        #region Pool
        /// <summary>
        /// Called by the pool just before the instance is returned to the pool.
        /// Shall be used to reset the state of this instance.
        /// </summary>
        [PublicAPI]
        public override void OnPrepareForReturnToPool() {
            #region TLP_DEBUG
#if TLP_DEBUG
            DebugLog(nameof(OnPrepareForReturnToPool));
#endif
#endregion
            if (!Clear()) {
                Destroy(gameObject);
                return;
            }
            gameObject.name = nameof(AvlTree);
        }
        #endregion

        public bool Clear() {
            int size = Size;
            for (int i = 0; i < size; i++) {
                if (!Remove(Get(0))) {
                    Error($"{nameof(Clear)}: Failed to remove first element");
                    return false;
                }
            }

            if (Size != 0) {
                Error($"{nameof(Clear)}: Failed to clear the {this.GetScriptPathInScene()}");
                return false;
            }

            RootNode = null;
            NodePool.Clear();
            return true;
        }
    }
}