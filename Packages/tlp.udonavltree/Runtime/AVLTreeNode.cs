using System;
using System.Runtime.CompilerServices;
using JetBrains.Annotations;
using TLP.UdonUtils.Runtime;
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

namespace TLP.UdonAVLTree.Runtime
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    // ReSharper disable once InconsistentNaming
    public class AVLTreeNode : TlpBaseBehaviour
    {
        [HideInInspector]
        public AVLTreeNode parent;

        public TlpBaseBehaviour payload;

        [HideInInspector]
        public AVLTreeNode left;

        [HideInInspector]
        public AVLTreeNode right;

        public int Balance => (IsLeftValidNode() ? left.height : 0) - (IsRightValidNode() ? right.height : 0);

        [HideInInspector]
        public bool leftIsWire, rightIsWire;

        [HideInInspector]
        public int count, height;

        public Transform ownTransform;

        [PublicAPI]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void UpdateValues()
        {
            count = (IsLeftValidNode() ? left.count : 0) + (IsRightValidNode() ? right.count : 0) + 1;
            height = Math.Max(IsLeftValidNode() ? left.height : 0, IsRightValidNode() ? right.height : 0) + 1;
        }

        [PublicAPI]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool HasChildren()
        {
            return IsLeftValidNode() || IsRightValidNode();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsLeftValidNode()
        {
            return !leftIsWire && Utilities.IsValid(left);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsRightValidNode()
        {
            return !rightIsWire && Utilities.IsValid(right);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public AVLTreeNode RotateLeft()
        {
            return Rotate(true);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public AVLTreeNode RotateRight()
        {
            return Rotate(false);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private AVLTreeNode Rotate(bool rotateLeft)
        {
            if (rotateLeft ? Balance < -2 || Balance > -1 : Balance > 2 || Balance < 1)
            {
                Error($"RotateLeft: The tree is not {(rotateLeft ? "right" : "left")}-heavy!");
                return this;
            }

            var rootParent = parent;
            var rootTransform = transform.parent;
            AVLTreeNode a;
            a = this; //  gameObject.GetComponent<AVLTreeNode>(); // TODO provide via parameter
            var c = rotateLeft ? a.right : a.left;
            var b = rotateLeft ? c.IsLeftValidNode() ? c.left : null : c.IsRightValidNode() ? c.right : null;
            var d = rotateLeft ? c.IsRightValidNode() ? c.right : null : c.IsLeftValidNode() ? c.left : null;


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


            if (b)
            {
                b.parent = a;
                b.ownTransform.parent = a.ownTransform;

                if (rotateLeft)
                {
                    a.right = b;
                    a.rightIsWire = false;
                    ConnectLeftWire(b, a);
                    ConnectRightWire(a, c);
                }
                else
                {
                    a.left = b;
                    a.leftIsWire = false;
                    ConnectRightWire(b, a);
                    ConnectLeftWire(a, c);
                }
            }
            else
            {
                if (rotateLeft)
                {
                    a.right = c;
                    a.rightIsWire = true;
                }
                else
                {
                    a.left = c;
                    a.leftIsWire = true;
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
            if (rotateLeft)
            {
                c.left = a;
                c.leftIsWire = false;
            }
            else
            {
                c.right = a;
                c.rightIsWire = false;
            }

            c.parent = rootParent;
            c.ownTransform.parent = rootTransform;

            a.parent = c;
            a.ownTransform.parent = c.ownTransform;


            if (d)
            {
                if (rotateLeft)
                {
                    ConnectLeftWire(d, c);
                }
                else
                {
                    ConnectRightWire(d, c);
                }
            }

            // update Balance of (A)
            a.UpdateValues();

            // and Balance of new root (C)
            c.UpdateValues();

            if (rootParent)
            {
                if (rootParent.left == a && rootParent.IsLeftValidNode())
                {
                    rootParent.left = c;
                }
                else
                {
                    rootParent.right = c;
                }
            }

            // return root (C) to allow replacing (A) as former root
            return c;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ConnectRightWire(AVLTreeNode firstNode, AVLTreeNode nextNodeInParents)
        {
            var bRight = GetLast(firstNode);

            bRight.right = nextNodeInParents;
            bRight.rightIsWire = true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ConnectLeftWire(AVLTreeNode firstNode, AVLTreeNode nextNodeInParents)
        {
            var bLeft = GetFirst(firstNode);

            bLeft.left = nextNodeInParents;
            bLeft.leftIsWire = true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public AVLTreeNode GetLast(AVLTreeNode d)
        {
            var dLeft = d;
            int cnt = 0;
            while (dLeft.IsRightValidNode())
            {
                if (cnt++ > 1000)
                {
                    Error("GetMostRight: Potential endless loop detected");
                    return null;
                }

                dLeft = dLeft.right;
            }

            return dLeft;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public AVLTreeNode GetFirst(AVLTreeNode d)
        {
            var dLeft = d;
            int cnt = 0;
            while (dLeft.IsLeftValidNode())
            {
                if (cnt++ > 1000)
                {
                    Error("GetMostLeft: Potential endless loop detected");
                    return null;
                }

                dLeft = dLeft.left;
            }

            return dLeft;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public string ToStringWithChildren()
        {
            // first find the min node
            AVLTreeNode start = null;
            start = this;
            while (start.IsLeftValidNode())
            {
                start = start.left;
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

            do
            {
                while (current.IsLeftValidNode())
                {
                    current = current.left;
                }

                result = (string.IsNullOrEmpty(result) ? "" : result + ",") + current.PayloadToString();

                var previous = current;
                current = current.right;
                while (previous.rightIsWire && Utilities.IsValid(current))
                {
                    result = result + "," + current.PayloadToString();
                    previous = current;
                    current = current.right;
                }
            } while (Utilities.IsValid(current));

            return result;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public string PayloadToString()
        {
            if (Utilities.IsValid(payload))
            {
                return payload.ToString();
            }

            return "None";
        }

        #region UdonPool Interface

        /// <summary>
        /// Called by the pool just before the instance is returned to the pool.
        /// Shall be used to reset the state of this instance.
        /// </summary>
        [PublicAPI]
        public override void OnPrepareForReturnToPool()
        {
            gameObject.name = nameof(AVLTreeNode);
            left = null;
            right = null;
            leftIsWire = false;
            rightIsWire = false;
            count = 0;
            height = 0;
            payload = null;
        }

        #endregion

        public void SetParent(Transform parentTransform)
        {
            transform.parent = parentTransform;
        }
    }
}