using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;
using JetBrains.Annotations;
using TLP.UdonUtils;
using TLP.UdonUtils.Common;
using TLP.UdonUtils.Factories;
using TLP.UdonUtils.Runtime.Pool;
using UdonSharp;
using UnityEngine;
using UnityEngine.Serialization;
using VRC.SDKBase;
using VRC.Udon;

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
    public class AVLTree : TlpBaseBehaviour
    {
        protected override int ExecutionOrderReadOnly => ExecutionOrder;

        [PublicAPI]
        public new const int ExecutionOrder = FactoryWithPool.ExecutionOrder + 1;

        public int Size { get; internal set; }

        [FormerlySerializedAs("comparer")]
        public Comparer Comparer;

        [FormerlySerializedAs("treeNodes")]
        public Transform TreeNodes;

        internal Pool _avlTreeNodePool;
        private FactoryWithPool _nodeFactory;

        internal AVLTreeNode RootNode;


        public override void Start()
        {
            base.Start();
            if (!Utilities.IsValid(Comparer))
            {
                Error($"{nameof(Comparer)} invalid");
                enabled = false;
                return;
            }

            if (!Utilities.IsValid(_nodeFactory))
            {
                _nodeFactory = TlpFactory.GetConcreteFactory<FactoryWithPool>(nameof(AVLTreeNode));
            }

            if (!Utilities.IsValid(_nodeFactory))
            {
                Error($"{nameof(_nodeFactory)} invalid");
                enabled = false;
                return;
            }

            _avlTreeNodePool = _nodeFactory.ProductPool;
            if (!Utilities.IsValid(_avlTreeNodePool))
            {
                Error($"{nameof(_avlTreeNodePool)} invalid");
                enabled = false;
                return;
            }
        }


        public bool Add(TlpBaseBehaviour newElement)
        {
#if TLP_DEBUG
            DebugLog(nameof(Add));
#endif

            if (!Utilities.IsValid(_avlTreeNodePool))
            {
                Error($"{nameof(_avlTreeNodePool)} invalid");
                return false;
            }

            var newNodeGameObject = _avlTreeNodePool.Get();
            if (!Utilities.IsValid(newNodeGameObject))
            {
                Error("Failed to get node from pool");
                return false;
            }

            var newNode = newNodeGameObject.GetComponent<AVLTreeNode>();
            if (!Utilities.IsValid(newNode))
            {
                Error("New node does not have a AVLTreeNode component");
                _avlTreeNodePool.Return(newNodeGameObject);
                return false;
            }

            newNode.payload = newElement;

            var current = RootNode;

            while (Utilities.IsValid(current))
            {
                // ReSharper disable once InlineOutVariableDeclaration not supported yet by Udon
                int comparisonResult;
                bool comparisonSuccess = Comparer.Compare(newElement, current.payload, out comparisonResult);
                if (!comparisonSuccess)
                {
                    Error("Add failed on comparison");
                    _avlTreeNodePool.Return(newNodeGameObject);
                    return false;
                }

                if (comparisonResult == -1)
                {
                    if (!current.IsLeftValidNode())
                    {
                        // set parent
                        newNode.ownTransform.parent = current.ownTransform;
                        newNode.parent = current;

                        // take of parents left wire
                        newNode.left = current.left;
                        newNode.leftIsWire = current.leftIsWire;

                        // connect the right wire to the parent
                        newNode.right = current;
                        newNode.rightIsWire = true;

                        // attach to the parents left side
                        current.left = newNode;
                        current.leftIsWire = false;

                        // leave for balancing
                        current = newNode;
                        break;
                    }

                    current = current.left;
                }
                else
                {
                    if (!current.IsRightValidNode())
                    {
                        // set parent
                        newNode.ownTransform.parent = current.ownTransform;
                        newNode.parent = current;

                        // take of parents right wire
                        newNode.right = current.right;
                        newNode.rightIsWire = current.rightIsWire;

                        // connect the left wire to the parent
                        newNode.left = current;
                        newNode.leftIsWire = true;

                        // attach to the parents right side
                        current.right = newNode;
                        current.rightIsWire = false;

                        // leave for balancing
                        current = newNode;
                        break;
                    }

                    current = current.right;
                }
            }

            if (!Utilities.IsValid(current))
            {
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

        public bool Remove(UdonSharpBehaviour elementToRemove)
        {
            DebugLog(nameof(Remove));
            if (!Utilities.IsValid(elementToRemove))
            {
                Error(nameof(elementToRemove));
                return false;
            }

            var nodeToDelete = FindNode(elementToRemove, RootNode);

            if (!Utilities.IsValid(nodeToDelete))
            {
                // the value does not exist in tree
                Warn($"Value {elementToRemove.ToString()} does not exist");
                return false;
            }

            bool leftValid = nodeToDelete.IsLeftValidNode();
            bool rightValid = nodeToDelete.IsRightValidNode();

            AVLTreeNode balanceStart = null;
            if (!rightValid && !leftValid)
            {
                balanceStart = RemoveLeafNode(nodeToDelete);
            }
            else if (nodeToDelete.Balance < 0)
            {
                balanceStart = RemoveRootNodeOfLeftHeavyTree(nodeToDelete, false);
            }
            else
            {
                balanceStart = RemoveRootNodeOfLeftHeavyTree(nodeToDelete, true);
            }

            if (Utilities.IsValid(balanceStart))
            {
                Balance(balanceStart);
            }
            else
            {
                RootNode = null;
            }

            Size--;

            return true;
        }

#if !COMPILER_UDONSHARP && UNITY_EDITOR

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

        public bool VerifyChildrenConnections()
        {
            var allNodes = TreeNodes.GetComponentsInChildren<AVLTreeNode>(true);
            var leftChildren = new HashSet<AVLTreeNode>();
            var rightChildren = new HashSet<AVLTreeNode>();

            foreach (var avlTreeNode in allNodes)
            {
                if (!FindNode(avlTreeNode.payload, avlTreeNode))
                {
                    return false;
                }

                if (avlTreeNode.IsLeftValidNode())
                {
                    if (leftChildren.Contains(avlTreeNode.left))
                    {
                        Error(avlTreeNode.PayloadToString() + " also links to " + avlTreeNode.left.PayloadToString());
                        return false;
                    }

                    leftChildren.Add(avlTreeNode.left);
                }

                if (avlTreeNode.IsRightValidNode())
                {
                    if (rightChildren.Contains(avlTreeNode.right))
                    {
                        Error(avlTreeNode.PayloadToString() + " also links to " + avlTreeNode.left.PayloadToString());
                        return false;
                    }

                    rightChildren.Add(avlTreeNode.right);
                }
            }

            return true;
        }

        internal bool VerifyBalance()
        {
            bool success = true;
            foreach (var node in TreeNodes.GetComponentsInChildren<AVLTreeNode>(true))
            {
                int balance = node.Balance;
                if (balance < -1 || balance > 1)
                {
                    Error(node.PayloadToString() + " has wrong balance " + balance);
                    success = false;
                }
            }

            return success;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void LogList(string prefix, List<AVLTreeNode> list)
        {
            var sb = new StringBuilder();
            sb.Append(prefix).Append("\n{");
            foreach (var i in list)
            {
                sb.Append(i.PayloadToString()).Append(",");
            }


            sb.Replace(",", "", sb.Length - 1, 1);
            sb.Append("}");

            Debug.Log(sb.ToString());
        }

        #endregion

#endif

        [PublicAPI]
        public UdonSharpBehaviour Contains(UdonSharpBehaviour searchElement)
        {
            DebugLog(nameof(Contains));
            var avlTreeNode = FindNode(searchElement, RootNode);
            if (Utilities.IsValid(avlTreeNode))
            {
                return avlTreeNode.payload;
            }

            return null;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal AVLTreeNode FindNode(UdonSharpBehaviour searchElement, AVLTreeNode start)
        {
            if (!Utilities.IsValid(Comparer))
            {
                Error($"{nameof(Comparer)} invalid");
                return null;
            }

            while (true)
            {
                if (!Utilities.IsValid(start))
                {
                    // node not found, first exit point
                    return null;
                }

                // ReSharper disable once InlineOutVariableDeclaration not supported yet by Udon
                int comparisonResult;

                // ReSharper disable once PossibleNullReferenceException False positive, see Utilities.IsValid(start)
                bool comparisonSuccess = Comparer.Compare(searchElement, start.payload, out comparisonResult);
                if (!comparisonSuccess)
                {
                    Error($"Comparison failed when looking for Node {searchElement}");

                    // second exit point
                    return null;
                }

                if (comparisonResult == 0)
                {
                    // entry found, third exit point
                    return start;
                }

                if (comparisonResult < 0)
                {
                    start = start.IsLeftValidNode() ? start.left : null;
                }
                else
                {
                    start = start.IsRightValidNode() ? start.right : null;
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private AVLTreeNode RemoveLeafNode(AVLTreeNode nodeToDelete)
        {
            var successor = nodeToDelete.parent;
            if (Utilities.IsValid(successor))
            {
                if (successor.left == nodeToDelete)
                {
                    successor.left = nodeToDelete.left;
                    successor.leftIsWire = nodeToDelete.leftIsWire;
                }
                else
                {
                    successor.right = nodeToDelete.right;
                    successor.rightIsWire = nodeToDelete.rightIsWire;
                }
            }
            else
            {
                RootNode = null;
            }

            _avlTreeNodePool.Return(nodeToDelete.gameObject);
            return successor;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private AVLTreeNode RemoveNodeWithValidLeftChild(AVLTreeNode nodeToRemove)
        {
            var successor = nodeToRemove.left;

            // tell child about new parent
            successor.parent = nodeToRemove.parent;
            successor.ownTransform.parent = nodeToRemove.ownTransform.parent;

            bool childWillHaveParent = Utilities.IsValid(nodeToRemove.parent);
            if (childWillHaveParent)
            {
                bool deletedNodeWasLeftChild = nodeToRemove.parent.left == nodeToRemove;
                if (deletedNodeWasLeftChild)
                {
                    // attach successor to parent
                    nodeToRemove.parent.left = successor;
                }
                else
                {
                    // attach successor to parent
                    nodeToRemove.parent.right = successor;
                }

                // use the left wire of the deleted node
                successor.right = nodeToRemove.parent;
                successor.rightIsWire = true;
            }
            else
            {
                // successor becomes new root with no children/wires
                RootNode = successor;
                successor.left = null;
                successor.right = null;
                successor.leftIsWire = false;
                successor.rightIsWire = false;
            }

            _avlTreeNodePool.Return(nodeToRemove.gameObject);

            return successor;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private AVLTreeNode RemoveNodeWithValidRightChild(AVLTreeNode nodeToRemove)
        {
            var successor = nodeToRemove.right;

            // tell child about new parent
            successor.parent = nodeToRemove.parent;
            successor.ownTransform.parent = nodeToRemove.ownTransform.parent;

            bool childWillHaveParent = Utilities.IsValid(nodeToRemove.parent);
            if (childWillHaveParent)
            {
                // tell the parent about the new child
                bool deletedNodeWasLeftChild = nodeToRemove.parent.left == nodeToRemove;
                if (deletedNodeWasLeftChild)
                {
                    // attach successor to parent
                    nodeToRemove.parent.left = successor;
                }
                else
                {
                    // attach successor to parent
                    nodeToRemove.parent.right = successor;
                }

                // use the left wire of the deleted node
                successor.left = nodeToRemove.parent;
            }
            else
            {
                // successor becomes new root with no children/wires
                RootNode = successor;
                successor.left = null;
                successor.right = null;
                successor.leftIsWire = false;
                successor.rightIsWire = false;
            }

            _avlTreeNodePool.Return(nodeToRemove.gameObject);
            return successor;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal AVLTreeNode RemoveNodeWithValidChildren(AVLTreeNode nodeToRemove)
        {
            if (nodeToRemove.Balance < 0)
            {
                nodeToRemove = RemoveRootNodeOfLeftHeavyTree(nodeToRemove, false);
            }
            else
            {
                nodeToRemove = RemoveRootNodeOfLeftHeavyTree(nodeToRemove, true);
            }

            return nodeToRemove;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal AVLTreeNode RemoveRootNodeOfRightHeavyTree(AVLTreeNode toRemove)
        {
            var successor = toRemove.GetFirst(toRemove.right);

            AVLTreeNode balancingStart;

            bool successorHasLeftChildren = successor != toRemove.right;
            if (successorHasLeftChildren)
            {
                balancingStart = successor.parent;

                if (successor.IsRightValidNode())
                {
                    // attach child of successor to parent of successor
                    var childOfSuccessor = successor.right;

                    if (balancingStart != childOfSuccessor)
                    {
                        balancingStart.left = childOfSuccessor;
                        childOfSuccessor.parent = balancingStart;
                        childOfSuccessor.ownTransform.parent = balancingStart.ownTransform;
                    }
                }
                else
                {
                    // connect the right tree wire to the new root
                    balancingStart.left = successor;
                    balancingStart.leftIsWire = true;
                }

                successor.right = toRemove.right;
                successor.rightIsWire = false;

                successor.parent = toRemove.parent;
                successor.ownTransform.parent = toRemove.ownTransform.parent;

                toRemove.left.parent = successor;
                toRemove.left.ownTransform.parent = successor.ownTransform;
                successor.left = toRemove.left;
                successor.leftIsWire = false;

                toRemove.right.parent = successor;
                toRemove.right.ownTransform.parent = successor.ownTransform;
            }
            else
            {
                balancingStart = successor;

                successor.parent = toRemove.parent;
                successor.ownTransform.parent = toRemove.ownTransform.parent;

                toRemove.left.parent = successor;
                toRemove.left.ownTransform.parent = successor.ownTransform;
                successor.left = toRemove.left;
                successor.leftIsWire = false;
            }

            // connect the right trees wire to the new root
            successor.GetLast(successor.left).right = successor;
            successor.GetLast(successor.left).rightIsWire = true;

            if (Utilities.IsValid(toRemove.parent))
            {
                if (toRemove.parent.left == toRemove)
                {
                    toRemove.parent.left = successor;

                    var mostRight = successor.GetLast(successor);
                    mostRight.right = toRemove.parent;
                    mostRight.rightIsWire = true;
                }
                else
                {
                    toRemove.parent.right = successor;
                    var mostLeft = successor.GetFirst(successor);
                    mostLeft.left = toRemove.parent;
                    mostLeft.leftIsWire = true;
                }
            }
            else
            {
                var mostLeft = successor.GetFirst(successor);
                mostLeft.left = null;
                mostLeft.leftIsWire = false;

                var mostRight = successor.GetLast(successor);
                mostRight.right = null;
                mostRight.rightIsWire = false;
            }

            _avlTreeNodePool.Return(toRemove.gameObject);

            return balancingStart;
        }

        /// <summary>
        /// given a node that is the root of a (sub-) tree it replaces it with the
        /// single left child. Must only have a single node in the left side!
        /// </summary>
        /// <param name="root"></param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal AVLTreeNode ReplaceRootWithLeftChild(AVLTreeNode root)
        {
            if (root.IsRightValidNode())
            {
                var minRight = root.right.GetFirst(root.right);

                root.right.parent = root.left;
                root.right.ownTransform.parent = root.left.ownTransform;

                minRight.left = root.left;
                minRight.leftIsWire = true;
            }

            root.left.parent = root.parent;
            root.left.ownTransform.parent = root.ownTransform.parent;

            root.left.right = root.right;
            root.left.rightIsWire = root.rightIsWire;

            if (root == RootNode)
            {
                RootNode = root.left;
            }
            else
            {
                if (root.parent.left == root)
                {
                    root.parent.left = root.left;
                }
                else
                {
                    root.parent.right = root.left;
                }
            }

            return root.left;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal AVLTreeNode ReplaceRootWithRightChild(AVLTreeNode root)
        {
            if (root.IsLeftValidNode())
            {
                var maxLeft = root.left.GetLast(root.left);

                root.left.parent = root.right;
                root.left.ownTransform.parent = root.right.ownTransform;

                maxLeft.right = root.right;
                maxLeft.rightIsWire = true;
            }

            root.right.parent = root.parent;
            root.right.ownTransform.parent = root.ownTransform.parent;

            root.right.left = root.left;
            root.right.leftIsWire = root.leftIsWire;

            if (root == RootNode)
            {
                RootNode = root.right;
            }
            else
            {
                if (root.parent.right == root)
                {
                    root.parent.right = root.right;
                }
                else
                {
                    root.parent.left = root.right;
                }
            }

            return root.right;
        }

        /// <summary>
        /// B -> C
        ///     C
        ///   B   (?)
        /// A    (?)
        /// </summary>
        /// <param name="root"></param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal AVLTreeNode ReplaceRootWithLeftTreeLeftHeavy(AVLTreeNode root)
        {
            var maxLeftTree = root.left.GetLast(root.left);
            var minRightTree = root.right.GetFirst(root.right);

            if (maxLeftTree.IsLeftValidNode())
            {
                maxLeftTree.RotateRight();
            }

            var balancingStartNode = maxLeftTree.parent;

            maxLeftTree.parent = root.parent;
            maxLeftTree.ownTransform.parent = root.ownTransform.parent;

            maxLeftTree.left = root.left;
            maxLeftTree.leftIsWire = false;


            maxLeftTree.right = root.right;
            maxLeftTree.rightIsWire = root.rightIsWire;

            minRightTree.left = maxLeftTree;

            root.right.parent = maxLeftTree;
            root.right.ownTransform.parent = maxLeftTree.ownTransform;

            if (balancingStartNode == root.left)
            {
                balancingStartNode.parent = maxLeftTree;
                balancingStartNode.ownTransform.parent = maxLeftTree.ownTransform;
            }

            balancingStartNode.rightIsWire = true;


            root.left.parent = maxLeftTree;
            root.left.ownTransform.parent = maxLeftTree.ownTransform;

            if (root.parent)
            {
                if (root.parent.left == root)
                {
                    root.parent.left = maxLeftTree;
                }
                else
                {
                    root.parent.right = maxLeftTree;
                }
            }

            return balancingStartNode;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal AVLTreeNode ReplaceRootWithRightTreeRightHeavy(AVLTreeNode root)
        {
            var minRightTree = root.right.GetFirst(root.right);
            var maxLeftTree = root.left.GetLast(root.left);

            if (minRightTree.IsRightValidNode())
            {
                minRightTree.RotateLeft();
            }

            var balancingStartNode = minRightTree.parent;

            minRightTree.parent = root.parent;
            minRightTree.ownTransform.parent = root.ownTransform.parent;

            minRightTree.right = root.right;
            minRightTree.rightIsWire = false;


            minRightTree.left = root.left;
            minRightTree.leftIsWire = root.leftIsWire;

            maxLeftTree.right = minRightTree;

            root.left.parent = minRightTree;
            root.left.ownTransform.parent = minRightTree.ownTransform;

            if (balancingStartNode == root.right)
            {
                balancingStartNode.parent = minRightTree;
                balancingStartNode.ownTransform.parent = minRightTree.ownTransform;
            }

            balancingStartNode.leftIsWire = true;


            root.right.parent = minRightTree;
            root.right.ownTransform.parent = minRightTree.ownTransform;

            if (root.parent)
            {
                if (root.parent.right == root)
                {
                    root.parent.right = minRightTree;
                }
                else
                {
                    root.parent.left = minRightTree;
                }
            }

            return balancingStartNode;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal AVLTreeNode RemoveRootNodeOfLeftHeavyTree(AVLTreeNode toRemove, bool isLeft)
        {
            var successor = isLeft ? toRemove.left.GetLast(toRemove.left) : toRemove.right.GetFirst(toRemove.right);

            // left/right child has no right/left child
            if (isLeft ? successor == toRemove.left : successor == toRemove.right)
            {
                successor = isLeft
                    ? ReplaceRootWithLeftChild(toRemove)
                    : ReplaceRootWithRightChild(toRemove);
            }
            else
            {
                successor = isLeft
                    ? ReplaceRootWithLeftTreeLeftHeavy(toRemove)
                    : ReplaceRootWithRightTreeRightHeavy(toRemove);
            }

            _avlTreeNodePool.Return(toRemove.gameObject);

            return successor;
        }

#if !COMPILER_UDONSHARP && UNITY_EDITOR
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool CheckForCyclicParentRelations(AVLTreeNode tmp)
        {
            var visited = new HashSet<AVLTreeNode>();
            while (Utilities.IsValid(tmp))
            {
                if (visited.Contains(tmp))
                {
                    Error(tmp.PayloadToString() + "Already visited");
                    return true;
                }

                visited.Add(tmp);

                tmp = tmp.parent;
            }

            return false;
        }
#endif

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void Balance(AVLTreeNode begin)
        {
            for (var current = begin; Utilities.IsValid(current); current = current.parent)
            {
                RootNode = current;
                current.UpdateValues();

                if (current.Balance >= 2 && current.left.Balance >= 0) // left - left
                {
                    current = current.RotateRight();
                    RootNode = current;
                }
                else if (current.Balance >= 2)
                {
                    // left - right
                    current.left = current.left.RotateLeft();
                    current = current.RotateRight();
                    RootNode = current;
                }
                else if (current.Balance <= -2 && current.right.Balance <= 0) // right - right
                {
                    current = current.RotateLeft();
                    RootNode = current;
                }
                else if (current.Balance <= -2)
                {
                    // right - left
                    current.right = current.right.RotateRight();
                    current = current.RotateLeft();
                    RootNode = current;
                }
            }

            if (Utilities.IsValid(RootNode))
            {
                RootNode.SetParent(TreeNodes);
            }
            else
            {
                Info("Tree is empty, nothing to balance");
            }
        }

        public override string ToString()
        {
            return Utilities.IsValid(RootNode) ? RootNode.ToStringWithChildren() : "Empty";
        }

#if !COMPILER_UDONSHARP && UNITY_EDITOR
        public StringBuilder Display(AVLTreeNode cur, int depth = 0, int state = 0, StringBuilder sb = null)
        {
            if (sb == null)
            {
                sb = new StringBuilder();
            }

            if (!Utilities.IsValid(cur))
            {
                Error("Invalid node encountered");
                return sb;
            }

            // state: 1 -> left, 2 -> right , 0 -> root
            if (cur.IsLeftValidNode())
            {
                sb = Display(cur.left, depth + 1, 1, sb);
            }

            int count = 0;
            for (int i = 0; i < depth; i++)
            {
                if (count++ > 1000)
                {
                    Error("Balance: Potential endless loop detected");
                    return sb;
                }

                sb.Append("|    ");
            }

            if (state == 1) // left
            {
                sb.Append("┌───");
            }
            else if (state == 2) // right
            {
                sb.Append("└───");
            }

            sb.Append("[")
                .Append(cur.PayloadToString())
                .Append("](")
                .Append(cur.count.ToString())
                .Append(", ")
                .Append(cur.height)
                .Append(", ")
                .Append(cur.Balance)
                .Append(")");


            if (!cur.IsLeftValidNode())
            {
                sb.Append(" lW=" + (cur.left ? cur.left.PayloadToString() : "null"));
            }

            if (!cur.IsRightValidNode())
            {
                sb.Append(" rW=" + (cur.right ? cur.right.PayloadToString() : "null"));
            }

            sb.Append("\n");

            if (cur.IsRightValidNode())
            {
                sb = Display(cur.right, depth + 1, 2, sb);
            }

            return sb;
        }
#endif
        private int m_LastFrame;
        private int m_GetCallCount;

        public TlpBaseBehaviour Get(int index)
        {
#if TLP_DEBUG
            DebugLog(nameof(Get));
#endif
            int frame = Time.renderedFrameCount;
            if (frame != m_LastFrame)
            {
                m_LastFrame = frame;
                if (m_GetCallCount > 0)
                {
                    DebugLog($"Get was called {m_GetCallCount} times");
                    m_GetCallCount = 0;
                }
            }

            ++m_GetCallCount;


            if (index < 0 || index >= Size)
            {
                return null;
            }

            var current = RootNode;
            int left = current.IsLeftValidNode() ? current.left.count : 0;

            while (left != index)
            {
                if (left < index)
                {
                    index -= left + 1;

                    current = current.right;
                    left = current.IsLeftValidNode() ? current.left.count : 0;
                }

                else
                {
                    current = current.left;
                    left = current.IsLeftValidNode() ? current.left.count : 0;
                }
            }

            return current.payload;
        }

        public bool IsEmpty()
        {
            return Size < 1;
        }

        #region UdonPool Interface

        /// <summary>
        /// Called by the pool just before the instance is returned to the pool.
        /// Shall be used to reset the state of this instance.
        /// </summary>
        [PublicAPI]
        public override void OnPrepareForReturnToPool()
        {
            gameObject.name = nameof(AVLTree);
            while (Size > 0)
            {
                if (!Utilities.IsValid(Comparer))
                {
                    Destroy(RootNode);
                    Size = 0;
                    break;
                }

                if (!Remove(RootNode))
                {
                    Error(
                        $"{nameof(OnPrepareForReturnToPool)}: Failed to clear the {nameof(AVLTree)}, destroying Object"
                    );
                    Destroy(gameObject);
                    return;
                }
            }

            if (Utilities.IsValid(Comparer) && Comparer.PoolableInUse)
            {
                var comparerPool = (Pool)Comparer.Pool;
                if (Utilities.IsValid(comparerPool))
                {
                    comparerPool.Return(Comparer.gameObject);
                }
            }

            Comparer = null;
        }

        #endregion
    }
}