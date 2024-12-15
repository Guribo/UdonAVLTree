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

        public static DataList CreateNode(DataList nodePool) {
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

        public static void ReturnToPool(this DataList node, DataList pool) {
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

        internal static DataList GetParent(this DataList node) {
            var dataToken = node[ParentIndex];
            if (dataToken.IsNull) return null;
            return dataToken.DataList;
        }

        internal static void SetParent(this DataList node, DataList parent) {
            node[ParentIndex] = parent;
        }

        internal static TlpBaseBehaviour GetPayload(this DataList node) {
            return (TlpBaseBehaviour)node[PayloadIndex].Reference;
        }

        internal static void SetPayload(this DataList node, TlpBaseBehaviour payload) {
            node[PayloadIndex] = payload;
        }

        internal static DataList GetLeft(this DataList node) {
            var dataToken = node[LeftNodeIndex];
            if (dataToken.IsNull) return null;
            return dataToken.DataList;
        }

        internal static void SetLeft(this DataList node, DataList leftNode) {
            node[LeftNodeIndex] = leftNode;
        }

        internal static DataList GetRight(this DataList node) {
            var dataToken = node[RightNodeIndex];
            if (dataToken.IsNull) return null;
            return dataToken.DataList;
        }

        internal static void SetRight(this DataList node, DataList rightNode) {
            node[RightNodeIndex] = rightNode;
        }

        internal static bool LeftIsWire(this DataList node) {
            return node[LeftIsWireIndex].Boolean;
        }

        internal static void SetLeftIsWire(this DataList node, bool isWire) {
            node[LeftIsWireIndex] = isWire;
        }

        internal static bool RightIsWire(this DataList node) {
            return node[RightIsWireIndex].Boolean;
        }

        internal static void SetRightIsWire(this DataList node, bool isWire) {
            node[RightIsWireIndex] = isWire;
        }

        internal static int GetNodeCount(this DataList node) {
            return node[NodeCountIndex].Int;
        }

        internal static void SetNodeCount(this DataList node, int count) {
            node[NodeCountIndex] = count;
        }

        internal static int GetTreeHeight(this DataList node) {
            return node[TreeHeight].Int;
        }

        internal static void SetTreeHeight(this DataList node, int height) {
            node[TreeHeight] = height;
        }

        public static bool IsLeftValidNode(this DataList node) {
            return !node.LeftIsWire() && Utilities.IsValid(node.GetLeft());
        }


        public static bool IsRightValidNode(this DataList node) {
            return !node.RightIsWire() && Utilities.IsValid(node.GetRight());
        }

        public static void UpdateValues(this DataList node) {
            node.SetNodeCount(
                    (node.IsLeftValidNode()
                            ? node.GetLeft().GetNodeCount()
                            : 0) +
                    (node.IsRightValidNode()
                            ? node.GetRight().GetNodeCount()
                            : 0) + 1
            );
            node.SetTreeHeight(
                    Math.Max(
                            node.IsLeftValidNode()
                                    ? node.GetLeft().GetTreeHeight()
                                    : 0,
                            node.IsRightValidNode()
                                    ? node.GetRight().GetTreeHeight()
                                    : 0)
                    + 1);
        }

        public static bool HasChildren(this DataList node) {
            return node.IsLeftValidNode() || node.IsRightValidNode();
        }

        public static DataList RotateLeft(this DataList node) {
            return node.Rotate(true);
        }

        public static DataList RotateRight(this DataList node) {
            return node.Rotate(false);
        }

        public static int GetBalance(this DataList node) {
            return (node.IsLeftValidNode()
                           ? node.GetLeft().GetTreeHeight()
                           : 0)
                   - (node.IsRightValidNode()
                           ? node.GetRight().GetTreeHeight()
                           : 0);
        }


        private static DataList Rotate(this DataList node, bool rotateLeft) {
            var balance = node.GetBalance();
            if (rotateLeft ? balance < -2 || balance > -1 : balance > 2 || balance < 1) {
                TlpLogger.StaticError($"RotateLeft: The tree is not {(rotateLeft ? "right" : "left")}-heavy!", null);
                return node;
            }

            var rootParent = node.GetParent();
            DataList a;
            a = node;
            var c = rotateLeft ? a.GetRight() : a.GetLeft();
            var b = rotateLeft ? c.IsLeftValidNode() ? c.GetLeft() : null :
                    c.IsRightValidNode() ? c.GetRight() : null;
            var d = rotateLeft ? c.IsRightValidNode() ? c.GetRight() : null :
                    c.IsLeftValidNode() ? c.GetLeft() : null;


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
                b.SetParent(a);

                if (rotateLeft) {
                    a.SetRight(b);
                    a.SetRightIsWire(false);
                    ConnectLeftWire(b, a);
                    ConnectRightWire(a, c);
                } else {
                    a.SetLeft(b);
                    a.SetLeftIsWire(false);
                    ConnectRightWire(b, a);
                    ConnectLeftWire(a, c);
                }
            } else {
                if (rotateLeft) {
                    a.SetRight(c);
                    a.SetRightIsWire(true);
                } else {
                    a.SetLeft(c);
                    a.SetLeftIsWire(true);
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
                c.SetLeft(a);
                c.SetLeftIsWire(false);
            } else {
                c.SetRight(a);
                c.SetRightIsWire(false);
            }

            c.SetParent(rootParent);

            a.SetParent(c);

            if (Utilities.IsValid(d)) {
                if (rotateLeft) {
                    ConnectLeftWire(d, c);
                } else {
                    ConnectRightWire(d, c);
                }
            }

            // update Balance of (A)
            a.UpdateValues();

            // and Balance of new root (C)
            c.UpdateValues();

            if (Utilities.IsValid(rootParent)) {
                if (ReferenceEquals(rootParent.GetLeft(), a) && rootParent.IsLeftValidNode()) {
                    rootParent.SetLeft(c);
                } else {
                    rootParent.SetRight(c);
                }
            }

            // return root (C) to allow replacing (A) as former root
            return c;
        }

        private static void ConnectRightWire(DataList firstNode, DataList nextNodeInParents) {
            var bRight = GetLast(firstNode);

            bRight.SetRight(nextNodeInParents);
            bRight.SetRightIsWire(true);
        }

        public static DataList GetLast(DataList d) {
            var dLeft = d;
            int cnt = 0;
            while (dLeft.IsRightValidNode()) {
                if (cnt++ > 1000) {
                    TlpLogger.StaticError("GetMostRight: Potential endless loop detected", null);
                    return null;
                }

                dLeft = dLeft.GetRight();
            }

            return dLeft;
        }

        public static DataList GetFirst(DataList d) {
            var dLeft = d;
            int cnt = 0;
            while (dLeft.IsLeftValidNode()) {
                if (cnt++ > 1000) {
                    TlpLogger.StaticError("GetMostLeft: Potential endless loop detected", null);
                    return null;
                }

                dLeft = dLeft.GetLeft();
            }

            return dLeft;
        }

        private static void ConnectLeftWire(DataList firstNode, DataList nextNodeInParents) {
            var bLeft = GetFirst(firstNode);

            bLeft.SetLeft(nextNodeInParents);
            bLeft.SetLeftIsWire(true);
        }

        public static string ToStringWithChildren(this DataList node) {
            // first find the min node
            DataList start = null;
            start = node;
            while (start.IsLeftValidNode()) {
                start = start.GetLeft();
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
                while (current.IsLeftValidNode()) {
                    current = current.GetLeft();
                }

                result = (string.IsNullOrEmpty(result) ? "" : result + ",") + current.PayloadToString();

                var previous = current;
                current = current.GetRight();
                while (previous.RightIsWire() && Utilities.IsValid(current)) {
                    result = result + "," + current.PayloadToString();
                    previous = current;
                    current = current.GetRight();
                }
            } while (Utilities.IsValid(current));

            return result;
        }

        public static string PayloadToString(this DataList node) {
            var tlpBaseBehaviour = node.GetPayload();
            if (Utilities.IsValid(tlpBaseBehaviour)) {
                return tlpBaseBehaviour.ToString();
            }

            return "None";
        }
    }
}