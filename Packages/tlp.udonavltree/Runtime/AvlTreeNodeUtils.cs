using System;
using TLP.UdonUtils.Runtime;
using TLP.UdonUtils.Runtime.Logger;
using VRC.SDK3.Data;
using VRC.SDKBase;

namespace TLP.UdonAVLTree.Runtime
{
    /// <summary>
    /// Definition of an AvlTreeNode, made up of a simple DataList
    ///
    /// Index   Type                Meaning
    /// -----------------------------------------
    /// 0       List                Parent
    /// 1       TlpBaseBehaviour    Payload
    /// 2       List                Left Node
    /// 3       List                Right Node
    /// 4       bool                Left is wire
    /// 5       bool                Right is wire
    /// 6       int                 Node count
    /// 7       int                 Tree height
    /// </summary>
    internal static class AvlTreeNodeUtils
    {
        private const int ParentIndex = 0;
        private const int PayloadIndex = 1;
        private const int LeftNodeIndex = 2;
        private const int RightNodeIndex = 3;
        private const int LeftIsWireIndex = 4;
        private const int RightIsWireIndex = 5;
        private const int NodeCountIndex = 6;
        private const int TreeHeight = 7;

        public static DataList _CreateNode(DataList nodePool) {
            DataList node;
            if (nodePool.Count > 0) {
                int lastIndex = nodePool.Count - 1;
                node = nodePool[lastIndex].DataList;
                nodePool.RemoveAt(lastIndex);
            } else {
                node = new DataList();
                node.Capacity = 8;
                node.Add((DataList)null); // ParentIndex
                node.Add((TlpBaseBehaviour)null); // PayloadIndex
                node.Add((DataList)null); // LeftNodeIndex
                node.Add((DataList)null); // RightNodeIndex
                node.Add(false); // LeftIsWireIndex
                node.Add(false); // RightIsWireIndex
                node.Add(0); // NodeCountIndex
                node.Add(0); // TreeHeight
            }

            return node;
        }

        public static void _ReturnToPool(this DataList node, DataList pool) {
            node.Add((DataList)null); // ParentIndex
            node.Add((TlpBaseBehaviour)null); // PayloadIndex
            node.Add((DataList)null); // LeftNodeIndex
            node.Add((DataList)null); // RightNodeIndex
            node.Add(false); // LeftIsWireIndex
            node.Add(false); // RightIsWireIndex
            node.Add(0); // NodeCountIndex
            node.Add(0); // TreeHeight
            pool.Add(node);
        }

        internal static DataList _GetParent(this DataList node) {
            var dataToken = node[ParentIndex];
            if (dataToken.IsNull) return null;
            return dataToken.DataList;
        }

        internal static void _SetParent(this DataList node, DataList parent) {
            node[ParentIndex] = parent;
        }

        internal static TlpBaseBehaviour _GetPayload(this DataList node) {
            return (TlpBaseBehaviour)node[PayloadIndex].Reference;
        }

        internal static void _SetPayload(this DataList node, TlpBaseBehaviour payload) {
            node[PayloadIndex] = payload;
        }

        internal static DataList _GetLeft(this DataList node) {
            var dataToken = node[LeftNodeIndex];
            if (dataToken.IsNull) return null;
            return dataToken.DataList;
        }

        internal static void _SetLeft(this DataList node, DataList leftNode) {
            node[LeftNodeIndex] = leftNode;
        }

        internal static DataList _GetRight(this DataList node) {
            var dataToken = node[RightNodeIndex];
            if (dataToken.IsNull) return null;
            return dataToken.DataList;
        }

        internal static void _SetRight(this DataList node, DataList rightNode) {
            node[RightNodeIndex] = rightNode;
        }

        internal static bool _LeftIsWire(this DataList node) {
            return node[LeftIsWireIndex].Boolean;
        }

        internal static void _SetLeftIsWire(this DataList node, bool isWire) {
            node[LeftIsWireIndex] = isWire;
        }

        internal static bool _RightIsWire(this DataList node) {
            return node[RightIsWireIndex].Boolean;
        }

        internal static void _SetRightIsWire(this DataList node, bool isWire) {
            node[RightIsWireIndex] = isWire;
        }

        internal static int _GetNodeCount(this DataList node) {
            return node[NodeCountIndex].Int;
        }

        internal static void _SetNodeCount(this DataList node, int count) {
            node[NodeCountIndex] = count;
        }

        internal static int _GetTreeHeight(this DataList node) {
            return node[TreeHeight].Int;
        }

        internal static void _SetTreeHeight(this DataList node, int height) {
            node[TreeHeight] = height;
        }

        public static bool _IsLeftValidNode(this DataList node) {
            return !node._LeftIsWire() && Utilities.IsValid(node._GetLeft());
        }


        public static bool _IsRightValidNode(this DataList node) {
            return !node._RightIsWire() && Utilities.IsValid(node._GetRight());
        }

        public static void _UpdateValues(this DataList node) {
            node._SetNodeCount(
                    (node._IsLeftValidNode()
                            ? node._GetLeft()._GetNodeCount()
                            : 0) +
                    (node._IsRightValidNode()
                            ? node._GetRight()._GetNodeCount()
                            : 0) + 1
            );
            node._SetTreeHeight(
                    Math.Max(
                            node._IsLeftValidNode()
                                    ? node._GetLeft()._GetTreeHeight()
                                    : 0,
                            node._IsRightValidNode()
                                    ? node._GetRight()._GetTreeHeight()
                                    : 0)
                    + 1);
        }

        public static bool _HasChildren(this DataList node) {
            return node._IsLeftValidNode() || node._IsRightValidNode();
        }

        public static DataList _RotateLeft(this DataList node) {
            return node._Rotate(true);
        }

        public static DataList _RotateRight(this DataList node) {
            return node._Rotate(false);
        }

        public static int _GetBalance(this DataList node) {
            return (node._IsLeftValidNode()
                           ? node._GetLeft()._GetTreeHeight()
                           : 0)
                   - (node._IsRightValidNode()
                           ? node._GetRight()._GetTreeHeight()
                           : 0);
        }


        private static DataList _Rotate(this DataList node, bool rotateLeft) {
            var balance = node._GetBalance();
            if (rotateLeft ? balance < -2 || balance > -1 : balance > 2 || balance < 1) {
                TlpLogger.StaticError($"RotateLeft: The tree is not {(rotateLeft ? "right" : "left")}-heavy!", null);
                return node;
            }

            var rootParent = node._GetParent();
            DataList a;
            a = node;
            var c = rotateLeft ? a._GetRight() : a._GetLeft();
            var b = rotateLeft ? c._IsLeftValidNode() ? c._GetLeft() : null :
                    c._IsRightValidNode() ? c._GetRight() : null;
            var d = rotateLeft ? c._IsRightValidNode() ? c._GetRight() : null :
                    c._IsLeftValidNode() ? c._GetLeft() : null;


            /*
             * Transfer (B) to the left tree side by attaching it to the current tree root (A)
             * Before:
             *   A[-2]
             *     \
             *    C[0]
             *  /    \
             * B[0]  D[0]
             *
             *
             * After:
             *  A[-2]
             *    \
             *   (B)[0]
             *
             *    C[0]
             *  /     \
             * B[0]  D[0]
             */


            if (Utilities.IsValid(b)) {
                b._SetParent(a);

                if (rotateLeft) {
                    a._SetRight(b);
                    a._SetRightIsWire(false);
                    _ConnectLeftWire(b, a);
                    _ConnectRightWire(a, c);
                } else {
                    a._SetLeft(b);
                    a._SetLeftIsWire(false);
                    _ConnectRightWire(b, a);
                    _ConnectLeftWire(a, c);
                }
            } else {
                if (rotateLeft) {
                    a._SetRight(c);
                    a._SetRightIsWire(true);
                } else {
                    a._SetLeft(c);
                    a._SetLeftIsWire(true);
                }
            }

            /*
             * Next: attach the tree root (A) to the left side of the previous right side (C) -> (C) is now new root
             *
             *     C[1]
             *   /     \
             * (A)[-1] D[0]
             *  \
             *   B[0]
             */
            if (rotateLeft) {
                c._SetLeft(a);
                c._SetLeftIsWire(false);
            } else {
                c._SetRight(a);
                c._SetRightIsWire(false);
            }

            c._SetParent(rootParent);

            a._SetParent(c);

            if (Utilities.IsValid(d)) {
                if (rotateLeft) {
                    _ConnectLeftWire(d, c);
                } else {
                    _ConnectRightWire(d, c);
                }
            }

            // update Balance of (A)
            a._UpdateValues();

            // and Balance of new root (C)
            c._UpdateValues();

            if (Utilities.IsValid(rootParent)) {
                if (ReferenceEquals(rootParent._GetLeft(), a) && rootParent._IsLeftValidNode()) {
                    rootParent._SetLeft(c);
                } else {
                    rootParent._SetRight(c);
                }
            }

            // return root (C) to allow replacing (A) as former root
            return c;
        }

        private static void _ConnectRightWire(DataList firstNode, DataList nextNodeInParents) {
            var bRight = _GetLast(firstNode);

            bRight._SetRight(nextNodeInParents);
            bRight._SetRightIsWire(true);
        }

        public static DataList _GetLast(DataList d) {
            var dLeft = d;
            int cnt = 0;
            while (dLeft._IsRightValidNode()) {
                if (cnt++ > 1000) {
                    TlpLogger.StaticError("GetMostRight: Potential endless loop detected", null);
                    return null;
                }

                dLeft = dLeft._GetRight();
            }

            return dLeft;
        }

        public static DataList _GetFirst(DataList d) {
            var dLeft = d;
            int cnt = 0;
            while (dLeft._IsLeftValidNode()) {
                if (cnt++ > 1000) {
                    TlpLogger.StaticError("GetMostLeft: Potential endless loop detected", null);
                    return null;
                }

                dLeft = dLeft._GetLeft();
            }

            return dLeft;
        }

        private static void _ConnectLeftWire(DataList firstNode, DataList nextNodeInParents) {
            var bLeft = _GetFirst(firstNode);

            bLeft._SetLeft(nextNodeInParents);
            bLeft._SetLeftIsWire(true);
        }

        public static string _ToStringWithChildren(this DataList node) {
            // first find the min node
            DataList start = null;
            start = node;
            while (start._IsLeftValidNode()) {
                start = start._GetLeft();
            }

            // find last node to consider
            // AVLTreeNode end = null;
            // end = this;
            // cnt = 0;
            // while (end.IsRightValidNode())
            // {
            //     if (cnt++ > 1000)
            //     {
            //         Error("ToStringWithChildren: IsRightValidNode: Potential endless loop detected");
            //         return null;
            //     }
            //
            //     end = end.right;
            // }

            string result = "";
            var current = start;

            do {
                while (current._IsLeftValidNode()) {
                    current = current._GetLeft();
                }

                result = (string.IsNullOrEmpty(result) ? "" : result + ",") + current._PayloadToString();

                var previous = current;
                current = current._GetRight();
                while (previous._RightIsWire() && Utilities.IsValid(current)) {
                    result = result + "," + current._PayloadToString();
                    previous = current;
                    current = current._GetRight();
                }
            } while (Utilities.IsValid(current));

            return result;
        }

        public static string _PayloadToString(this DataList node) {
            var tlpBaseBehaviour = node._GetPayload();
            if (Utilities.IsValid(tlpBaseBehaviour)) {
                return tlpBaseBehaviour.ToString();
            }

            return "None";
        }
    }
}