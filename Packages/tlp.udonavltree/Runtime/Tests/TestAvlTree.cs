using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using NUnit.Framework;
using TLP.UdonAVLTree.Runtime;
//using TLP.UdonLeaderBoard.Tests.Editor;
using TLP.UdonUtils.Editor.Tests;
using TLP.UdonUtils.Runtime.Pool;
using UnityEngine;
using UnityEngine.TestTools;
using Debug = UnityEngine.Debug;
using Random = UnityEngine.Random;

namespace TLP.UdonAVLTree.Tests.Runtime
{
    public class TestAvlTree : TestWithLogger
    {
        private AVLTree _avlTree;

        [SetUp]
        public override void Setup()
        {
            base.Setup();

            //var _ = new Prefabs();

            var avlTreeRoot = new GameObject("AVLTreeRoot");
            var avlTreeNodes = new GameObject("AVLTreeNodes");

            var avlTreeNodePrefab = new GameObject("AVLTreeNodePrefab");
            var avlTreeNodePool = new GameObject("AVLTreeNodePool");

            // attach model and element prefab to controller
            avlTreeNodes.transform.parent = avlTreeRoot.transform;
            avlTreeNodePrefab.transform.parent = avlTreeRoot.transform;
            avlTreeNodePool.transform.parent = avlTreeRoot.transform;

            _avlTree = avlTreeRoot.AddComponent<AVLTree>();
            _avlTree.TreeNodes = avlTreeNodes.transform;

            var pool = avlTreeNodePool.AddComponent<Pool>();
            avlTreeNodePrefab.AddComponent<AVLTreeNode>().ownTransform = avlTreeNodePrefab.transform;
            pool.PoolInstancePrefab = avlTreeNodePrefab;
            _avlTree._avlTreeNodePool = pool;

            _avlTree.Comparer = _avlTree.gameObject.AddComponent<MockComparableElementComparer>();

            Debug.Log("=========== Test Setup end ===========");
        }

        // A UnityTest behaves like a coroutine in Play Mode. In Edit Mode you can use
        // `yield return null;` to skip a frame.
        [UnityTest]
        public IEnumerator Test10000RandomElements()
        {
            var gos = new HashSet<MockComparableElement>();
            var root = new GameObject();
            int cnt = 0;
            var used = new List<int>();

            try
            {
                LogAssert.ignoreFailingMessages = true;


                for (int i = 0; i < 10000; i++)
                {
                    int value;
                    do
                    {
                        value = Random.Range(0, int.MaxValue);
                    } while (used.Contains(value));

                    var mockComparableElement = new GameObject().AddComponent<MockComparableElement>();
                    mockComparableElement.valueToCompare = value;
                    mockComparableElement.transform.parent = root.transform;
                    gos.Add(mockComparableElement);
                    used.Add(value);
                }
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                Assert.Fail();
            }

            var stopwatch = new Stopwatch();
            stopwatch.Restart();

            var logs = new List<string>();
            var previous = stopwatch.Elapsed;

            foreach (var go in gos)
            {
                if (cnt % 1000 == 0)
                {
                    try
                    {
                        var now = stopwatch.Elapsed;
                        logs.Add(now.ToString() + " " + (now - previous).ToString());
                        previous = now;
                    }
                    catch (Exception e)
                    {
                        Debug.LogException(e);
                        Assert.Fail();
                    }

                    yield return new WaitForEndOfFrame();
                }

                _avlTree.Add(go);
                ++cnt;
            }

            try
            {
                foreach (string log in logs)
                {
                    Debug.Log(log);
                }

                var stopwatchElapsed = stopwatch.Elapsed;
                Debug.Log(stopwatchElapsed);
                Debug.Log("Average = " + stopwatchElapsed.TotalSeconds / used.Count);

                Debug.Log("Size = " + _avlTree.Size);

                stopwatch.Restart();
                string benchResult = _avlTree.ToString();
                Debug.Log("reading all values = " + stopwatch.Elapsed);
                Debug.Log("All = " + benchResult);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                throw;
            }


            yield return null;
        }
    }
}