using System.Collections.Generic;
using System.Text;
using JetBrains.Annotations;
using TLP.UdonUtils.Runtime;
using TLP.UdonUtils.Runtime.Common;
using TLP.UdonUtils.Runtime.Player;
using UdonSharp;
using UnityEngine;
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
        #region ExecutionOrder
        public override int ExecutionOrderReadOnly => ExecutionOrder;

        [PublicAPI]
        public new const int ExecutionOrder = PlayerBlackList.ExecutionOrder + 1;
        #endregion

        #region Dependencies
        [Header("Dependencies")]
        [Tooltip(
                "The comparer used to compare elements in the tree. " +
                "Must compatible with the UdonsharpBehaviour type managed by the tree.")]
        public Comparer Comparer;
        #endregion

        #region State
        internal DataList RootNode;
        internal readonly DataList NodePool = new DataList();

#if TLP_DEBUG
        private int _debugLastFrame;
        private int _debugGetCallCount;
#endif
        #endregion

        #region Overrides
        protected override bool _SetupAndValidate() {
            if (!base._SetupAndValidate()) {
                return false;
            }

            return _IsSet(Comparer, nameof(Comparer));
        }

        /// <summary>
        /// Called by the pool just before the instance is returned to the pool.
        /// Shall be used to reset the state of this instance.
        /// </summary>
        [PublicAPI]
        public override void _OnPrepareForReturnToPool() {
            #region TLP_DEBUG
#if TLP_DEBUG
            _DebugLog(nameof(_OnPrepareForReturnToPool));
#endif
            #endregion

            if (!_Clear()) {
                Destroy(gameObject);
                return;
            }

            gameObject.name = nameof(AvlTree);
        }
        #endregion

        #region Public API
        /// <summary>
        /// Number of elements in the tree.
        /// </summary>
        public int Size { get; internal set; }

        public bool _Add(TlpBaseBehaviour newElement) {
            #region TLP_DEBUG
#if TLP_DEBUG
            _DebugLog($"{nameof(_Add)}: {newElement._GetScriptPathInScene()}");
#endif
            #endregion

            if (!HasStartedOk) {
                return false;
            }

            var newNode = AvlTreeNodeUtils._CreateNode(NodePool);
            newNode._SetPayload(newElement);

            var current = RootNode;

            while (Utilities.IsValid(current)) {
                // ReSharper disable once InlineOutVariableDeclaration not supported yet by Udon
                int comparisonResult;
                bool comparisonSuccess = Comparer._Compare(newElement, current._GetPayload(), out comparisonResult);
                if (!comparisonSuccess) {
                    _Error("Add failed on comparison");
                    newNode._ReturnToPool(NodePool);
                    return false;
                }

                if (comparisonResult == -1) {
                    if (!current._IsLeftValidNode()) {
                        // set parent
                        newNode._SetParent(current);

                        // take of parents left wire
                        newNode._SetLeft(current._GetLeft());
                        newNode._SetLeftIsWire(current._LeftIsWire());

                        // connect the right wire to the parent
                        newNode._SetRight(current);
                        newNode._SetRightIsWire(true);

                        // attach to the parents left side
                        current._SetLeft(newNode);
                        current._SetLeftIsWire(false);

                        // leave for balancing
                        current = newNode;
                        break;
                    }

                    current = current._GetLeft();
                } else {
                    if (!current._IsRightValidNode()) {
                        // set parent
                        newNode._SetParent(current);

                        // take of parents right wire
                        newNode._SetRight(current._GetRight());
                        newNode._SetRightIsWire(current._RightIsWire());

                        // connect the left wire to the parent
                        newNode._SetLeft(current);
                        newNode._SetLeftIsWire(true);

                        // attach to the parents right side
                        current._SetRight(newNode);
                        current._SetRightIsWire(false);

                        // leave for balancing
                        current = newNode;
                        break;
                    }

                    current = current._GetRight();
                }
            }

            if (!Utilities.IsValid(current)) {
                current = newNode;
            }

            _Balance(current);
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

        public bool _Remove(UdonSharpBehaviour elementToRemove) {
            #region TLP_DEBUG
#if TLP_DEBUG
            _DebugLog($"{nameof(_Remove)}: {elementToRemove._GetScriptPathInScene()}");
#endif
            #endregion

            if (!Utilities.IsValid(elementToRemove)) {
                _Error(nameof(elementToRemove));
                return false;
            }

            var nodeToDelete = _FindNode(elementToRemove, RootNode);

            if (!Utilities.IsValid(nodeToDelete)) {
                // the value does not exist in tree
                _Warn($"Value {elementToRemove} does not exist");
                return false;
            }

            bool leftValid = nodeToDelete._IsLeftValidNode();
            bool rightValid = nodeToDelete._IsRightValidNode();

            DataList balanceStart = null;
            if (!rightValid && !leftValid) {
                balanceStart = _RemoveLeafNode(nodeToDelete);
            } else if (nodeToDelete._GetBalance() < 0) {
                balanceStart = _RemoveRootNodeOfLeftHeavyTree(nodeToDelete, false);
            } else {
                balanceStart = _RemoveRootNodeOfLeftHeavyTree(nodeToDelete, true);
            }

            if (Utilities.IsValid(balanceStart)) {
                _Balance(balanceStart);
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
        public UdonSharpBehaviour _Contains(UdonSharpBehaviour searchElement) {
            #region TLP_DEBUG
#if TLP_DEBUG
            _DebugLog($"{nameof(_Contains)}: {searchElement._GetScriptPathInScene()}");
#endif
            #endregion

            var avlTreeNode = _FindNode(searchElement, RootNode);
            if (Utilities.IsValid(avlTreeNode)) {
                return avlTreeNode._GetPayload();
            }

            return null;
        }


        public StringBuilder _Display(DataList cur, int depth = 0, int state = 0, StringBuilder sb = null) {
            #region TLP_DEBUG
#if TLP_DEBUG
            _DebugLog(nameof(_Display));
#endif
            #endregion

            if (sb == null) {
                sb = new StringBuilder();
            }

            if (!Utilities.IsValid(cur)) {
                _Warn($"{nameof(_Display)}: Invalid node");
                return sb;
            }

            // state: 1 -> left, 2 -> right , 0 -> root
            if (cur._IsLeftValidNode()) {
                sb = _Display(cur._GetLeft(), depth + 1, 1, sb);
            }

            int count = 0;
            for (int i = 0; i < depth; i++) {
                if (count++ > 1000) {
                    _Error("Balance: Potential endless loop detected");
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
                    .Append(cur._PayloadToString())
                    .Append("](")
                    .Append(cur._GetNodeCount().ToString())
                    .Append(", ")
                    .Append(cur._GetTreeHeight())
                    .Append(", ")
                    .Append(cur._GetBalance())
                    .Append(")");


            if (!cur._IsLeftValidNode()) {
                sb.Append(" lW=" + (Utilities.IsValid(cur._GetLeft()) ? cur._GetLeft()._PayloadToString() : "null"));
            }

            if (!cur._IsRightValidNode()) {
                sb.Append(" rW=" + (Utilities.IsValid(cur._GetRight()) ? cur._GetRight()._PayloadToString() : "null"));
            }

            sb.Append("\n");

            if (cur._IsRightValidNode()) {
                sb = _Display(cur._GetRight(), depth + 1, 2, sb);
            }

            return sb;
        }


        public TlpBaseBehaviour _Get(int index) {
            #region TLP_DEBUG
#if TLP_DEBUG
            _DebugLog($"{nameof(_Get)}: {index}");
#endif
            #endregion

#if TLP_DEBUG
            int frame = Time.renderedFrameCount;
            if (frame != _debugLastFrame) {
                _debugLastFrame = frame;
                if (_debugGetCallCount > 0) {
                    #region TLP_DEBUG
                    _DebugLog($"Get was called {_debugGetCallCount} times");
                    #endregion

                    _debugGetCallCount = 0;
                }
            }

            ++_debugGetCallCount;
#endif


            if (index < 0 || index >= Size) {
                return null;
            }

            var current = RootNode;
            int left = current._IsLeftValidNode() ? current._GetLeft()._GetNodeCount() : 0;

            while (left != index) {
                if (left < index) {
                    index -= left + 1;

                    current = current._GetRight();
                    left = current._IsLeftValidNode() ? current._GetLeft()._GetNodeCount() : 0;
                } else {
                    current = current._GetLeft();
                    left = current._IsLeftValidNode() ? current._GetLeft()._GetNodeCount() : 0;
                }
            }

            return current._GetPayload();
        }

        public bool _IsEmpty() {
            return Size < 1;
        }


        public bool _Clear() {
            #region TLP_DEBUG
#if TLP_DEBUG
            _DebugLog(nameof(_Clear));
#endif
            #endregion

            int size = Size;
            for (int i = 0; i < size; i++) {
                if (!_Remove(_Get(0))) {
                    _Error($"{nameof(_Clear)}: Failed to remove first element");
                    return false;
                }
            }

            if (Size != 0) {
                _Error($"{nameof(_Clear)}: Failed to clear the {this._GetScriptPathInScene()}");
                return false;
            }

            RootNode = null;
            NodePool.Clear();
            return true;
        }

        public override string ToString() {
            return Utilities.IsValid(RootNode) ? RootNode._ToStringWithChildren() : "Empty";
        }
        #endregion

        #region Internal
        internal DataList _FindNode(UdonSharpBehaviour searchElement, DataList start) {
            if (!Utilities.IsValid(Comparer)) {
                _Error($"{nameof(Comparer)} invalid");
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
                bool comparisonSuccess = Comparer._Compare(searchElement, start._GetPayload(), out comparisonResult);
                if (!comparisonSuccess) {
                    _Error(
                            $"Comparison failed when looking for Node '{searchElement._GetScriptPathInScene()}' using comparer '{Comparer._GetScriptPathInScene()}'");

                    // second exit point
                    return null;
                }

                if (comparisonResult == 0) {
                    // entry found, third exit point
                    return start;
                }

                if (comparisonResult < 0) {
                    start = start._IsLeftValidNode() ? start._GetLeft() : null;
                } else {
                    start = start._IsRightValidNode() ? start._GetRight() : null;
                }
            }
        }


        private DataList _RemoveLeafNode(DataList nodeToDelete) {
            var successor = nodeToDelete._GetParent();
            if (Utilities.IsValid(successor)) {
                if (ReferenceEquals(successor._GetLeft(), nodeToDelete)) {
                    successor._SetLeft(nodeToDelete._GetLeft());
                    successor._SetLeftIsWire(nodeToDelete._LeftIsWire());
                } else {
                    successor._SetRight(nodeToDelete._GetRight());
                    successor._SetRightIsWire(nodeToDelete._RightIsWire());
                }
            } else {
                RootNode = null;
            }

            nodeToDelete._ReturnToPool(NodePool);
            return successor;
        }

        internal DataList _RemoveRootNodeOfRightHeavyTree(DataList toRemove) {
            var successor = AvlTreeNodeUtils._GetFirst(toRemove._GetRight());

            DataList balancingStart;

            bool successorHasLeftChildren = !ReferenceEquals(successor, toRemove._GetRight());
            if (successorHasLeftChildren) {
                balancingStart = successor._GetParent();

                if (successor._IsRightValidNode()) {
                    // attach child of successor to parent of successor
                    var childOfSuccessor = successor._GetRight();

                    if (!ReferenceEquals(balancingStart, childOfSuccessor)) {
                        balancingStart._SetLeft(childOfSuccessor);
                        childOfSuccessor._SetParent(balancingStart);
                    }
                } else {
                    // connect the right tree wire to the new root
                    balancingStart._SetLeft(successor);
                    balancingStart._SetLeftIsWire(true);
                }

                successor._SetRight(toRemove._GetRight());
                successor._SetRightIsWire(false);

                successor._SetParent(toRemove._GetParent());

                toRemove._GetLeft()._SetParent(successor);
                successor._SetLeft(toRemove._GetLeft());
                successor._SetLeftIsWire(false);

                toRemove._GetRight()._SetParent(successor);
            } else {
                balancingStart = successor;

                successor._SetParent(toRemove._GetParent());

                toRemove._GetLeft()._SetParent(successor);
                successor._SetLeft(toRemove._GetLeft());
                successor._SetLeftIsWire(false);
            }

            // connect the right trees wire to the new root
            AvlTreeNodeUtils._GetLast(successor._GetLeft())._SetRight(successor);
            AvlTreeNodeUtils._GetLast(successor._GetLeft())._SetRightIsWire(true);

            if (Utilities.IsValid(toRemove._GetParent())) {
                if (ReferenceEquals(toRemove._GetParent()._GetLeft(), toRemove)) {
                    toRemove._GetParent()._SetLeft(successor);

                    var mostRight = AvlTreeNodeUtils._GetLast(successor);
                    mostRight._SetRight(toRemove._GetParent());
                    mostRight._SetRightIsWire(true);
                } else {
                    toRemove._GetParent()._SetRight(successor);
                    var mostLeft = AvlTreeNodeUtils._GetFirst(successor);
                    mostLeft._SetLeft(toRemove._GetParent());
                    mostLeft._SetLeftIsWire(true);
                }
            } else {
                var mostLeft = AvlTreeNodeUtils._GetFirst(successor);
                mostLeft._SetLeft(null);
                mostLeft._SetLeftIsWire(false);

                var mostRight = AvlTreeNodeUtils._GetLast(successor);
                mostRight._SetRight(null);
                mostRight._SetRightIsWire(false);
            }

            RootNode = balancingStart;
            while (Utilities.IsValid(RootNode._GetParent())) {
                RootNode = RootNode._GetParent();
            }

            toRemove._ReturnToPool(NodePool);
            return balancingStart;
        }

        /// <summary>
        /// given a node that is the root of a (sub-) tree it replaces it with the
        /// single left child. Must only have a single node in the left side!
        /// </summary>
        /// <param name="root"></param>
        /// <returns></returns>
        internal DataList _ReplaceRootWithLeftChild(DataList root) {
            if (root._IsRightValidNode()) {
                var minRight = AvlTreeNodeUtils._GetFirst(root._GetRight());

                root._GetRight()._SetParent(root._GetLeft());

                minRight._SetLeft(root._GetLeft());
                minRight._SetLeftIsWire(true);
            }

            root._GetLeft()._SetParent(root._GetParent());


            root._GetLeft()._SetRight(root._GetRight());
            root._GetLeft()._SetRightIsWire(root._RightIsWire());

            if (ReferenceEquals(root, RootNode)) {
                RootNode = root._GetLeft();
            } else {
                if (ReferenceEquals(root._GetParent()._GetLeft(), root)) {
                    root._GetParent()._SetLeft(root._GetLeft());
                } else {
                    root._GetParent()._SetRight(root._GetLeft());
                }
            }

            return root._GetLeft();
        }


        internal DataList _ReplaceRootWithRightChild(DataList root) {
            if (root._IsLeftValidNode()) {
                var maxLeft = AvlTreeNodeUtils._GetLast(root._GetLeft());

                root._GetLeft()._SetParent(root._GetRight());

                maxLeft._SetRight(root._GetRight());
                maxLeft._SetRightIsWire(true);
            }

            root._GetRight()._SetParent(root._GetParent());

            root._GetRight()._SetLeft(root._GetLeft());
            root._GetRight()._SetLeftIsWire(root._LeftIsWire());

            if (ReferenceEquals(root, RootNode)) {
                RootNode = root._GetRight();
            } else {
                if (ReferenceEquals(root._GetParent()._GetRight(), root)) {
                    root._GetParent()._SetRight(root._GetRight());
                } else {
                    root._GetParent()._SetLeft(root._GetRight());
                }
            }

            return root._GetRight();
        }

        /// <summary>
        /// B -> C
        ///     C
        ///   B   (?)
        /// A    (?)
        /// </summary>
        /// <param name="root"></param>
        /// <returns></returns>
        internal DataList _ReplaceRootWithLeftTreeLeftHeavy(DataList root) {
            var maxLeftTree = AvlTreeNodeUtils._GetLast(root._GetLeft());
            var minRightTree = AvlTreeNodeUtils._GetFirst(root._GetRight());

            if (maxLeftTree._IsLeftValidNode()) {
                maxLeftTree._RotateRight();
            }

            var balancingStartNode = maxLeftTree._GetParent();

            maxLeftTree._SetParent(root._GetParent());

            maxLeftTree._SetLeft(root._GetLeft());
            maxLeftTree._SetLeftIsWire(false);


            maxLeftTree._SetRight(root._GetRight());
            maxLeftTree._SetRightIsWire(root._RightIsWire());

            minRightTree._SetLeft(maxLeftTree);

            root._GetRight()._SetParent(maxLeftTree);

            if (ReferenceEquals(balancingStartNode, root._GetLeft())) {
                balancingStartNode._SetParent(maxLeftTree);
            }

            balancingStartNode._SetRightIsWire(true);

            root._GetLeft()._SetParent(maxLeftTree);

            if (Utilities.IsValid(root._GetParent())) {
                if (ReferenceEquals(root._GetParent()._GetLeft(), root)) {
                    root._GetParent()._SetLeft(maxLeftTree);
                } else {
                    root._GetParent()._SetRight(maxLeftTree);
                }
            }

            return balancingStartNode;
        }


        internal DataList _ReplaceRootWithRightTreeRightHeavy(DataList root) {
            var minRightTree = AvlTreeNodeUtils._GetFirst(root._GetRight());
            var maxLeftTree = AvlTreeNodeUtils._GetLast(root._GetLeft());

            if (minRightTree._IsRightValidNode()) {
                minRightTree._RotateLeft();
            }

            var balancingStartNode = minRightTree._GetParent();

            minRightTree._SetParent(root._GetParent());

            minRightTree._SetRight(root._GetRight());
            minRightTree._SetRightIsWire(false);


            minRightTree._SetLeft(root._GetLeft());
            minRightTree._SetLeftIsWire(root._LeftIsWire());

            maxLeftTree._SetRight(minRightTree);

            root._GetLeft()._SetParent(minRightTree);

            if (ReferenceEquals(balancingStartNode, root._GetRight())) {
                balancingStartNode._SetParent(minRightTree);
            }

            balancingStartNode._SetLeftIsWire(true);


            root._GetRight()._SetParent(minRightTree);

            if (Utilities.IsValid(root._GetParent())) {
                if (ReferenceEquals(root._GetParent()._GetRight(), root)) {
                    root._GetParent()._SetRight(minRightTree);
                } else {
                    root._GetParent()._SetLeft(minRightTree);
                }
            }

            return balancingStartNode;
        }


        internal DataList _RemoveRootNodeOfLeftHeavyTree(DataList toRemove, bool isLeft) {
            var successor = isLeft
                    ? AvlTreeNodeUtils._GetLast(toRemove._GetLeft())
                    : AvlTreeNodeUtils._GetFirst(toRemove._GetRight());

            // left/right child has no right/left child
            if (isLeft
                        ? ReferenceEquals(successor, toRemove._GetLeft())
                        : ReferenceEquals(successor, toRemove._GetRight())) {
                successor = isLeft
                        ? _ReplaceRootWithLeftChild(toRemove)
                        : _ReplaceRootWithRightChild(toRemove);
            } else {
                successor = isLeft
                        ? _ReplaceRootWithLeftTreeLeftHeavy(toRemove)
                        : _ReplaceRootWithRightTreeRightHeavy(toRemove);
            }

            toRemove._ReturnToPool(NodePool);
            return successor;
        }

#if !COMPILER_UDONSHARP && UNITY_EDITOR

        private bool _CheckForCyclicParentRelations(DataList tmp) {
            var visited = new HashSet<DataList>();
            while (Utilities.IsValid(tmp)) {
                if (!visited.Add(tmp)) {
                    _Error(tmp._PayloadToString() + "Already visited");
                    return true;
                }

                tmp = tmp._GetParent();
            }

            return false;
        }
#endif


        private void _Balance(DataList begin) {
            for (var current = begin; Utilities.IsValid(current); current = current._GetParent()) {
                RootNode = current;
                current._UpdateValues();

                if (current._GetBalance() >= 2 && current._GetLeft()._GetBalance() >= 0) // left - left
                {
                    current = current._RotateRight();
                    RootNode = current;
                } else if (current._GetBalance() >= 2) {
                    // left - right
                    current._SetLeft(current._GetLeft()._RotateLeft());
                    current = current._RotateRight();
                    RootNode = current;
                } else if (current._GetBalance() <= -2 && current._GetRight()._GetBalance() <= 0) // right - right
                {
                    current = current._RotateLeft();
                    RootNode = current;
                } else if (current._GetBalance() <= -2) {
                    // right - left
                    current._SetRight(current._GetRight()._RotateRight());
                    current = current._RotateLeft();
                    RootNode = current;
                }
            }

            if (!Utilities.IsValid(RootNode)) {
                #region TLP_DEBUG
#if TLP_DEBUG
                _DebugLog("Tree is empty, nothing to balance");
#endif
                #endregion
            }
        }
        #endregion
    }
}