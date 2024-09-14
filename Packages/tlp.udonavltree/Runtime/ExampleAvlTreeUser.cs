using TLP.UdonUtils.Runtime;
using UdonSharp;
using UnityEngine;

namespace TLP.UdonAVLTree.Runtime
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class ExampleAvlTreeUser : TlpBaseBehaviour
    {
        public AVLTree avlTree;

        private Transform _ownTransform;

        private int _i = 0;
        public int count = 10000;

        private double avgAdd;
        private double avgRemove;
        private double factor;

        public override void Start()
        {
            base.Start();
            _ownTransform = transform;
            factor = 1.0 / count;

            var firstChild = _ownTransform.GetChild(0).gameObject.GetComponent<ExampleSortable>();
            for (int i = 0; i < count; i++)
            {
                var go = Instantiate(firstChild.gameObject);
                go.name = i.ToString();
                go.transform.parent = _ownTransform;
                go.GetComponent<ExampleSortable>().value = i;
            }

            Destroy(firstChild.gameObject);
            SendCustomEventDelayedSeconds(nameof(AddNext), 1);
        }

        public void AddNext()
        {
            if (_i < _ownTransform.childCount)
            {
                var stopwatch = new System.Diagnostics.Stopwatch();
                stopwatch.Restart();
                var o = _ownTransform.GetChild(_i).gameObject;
                Assert(avlTree.Add(o.GetComponent<ExampleSortable>()), $"Failed to add {o.name}");
                var stopwatchElapsed = stopwatch.Elapsed;
                avgAdd += stopwatchElapsed.TotalMilliseconds;
                if (_i % 100 == 0)
                {
                    Debug.Log(_i + ": avg add = " + avgAdd / _i + " ms");
                }

                ++_i;
                SendCustomEventDelayedFrames(nameof(AddNext), 1);
            }
            else
            {
                Debug.Log(_i + ": avg add = " + avgAdd / _i + " ms");
                Debug.Log(avlTree.ToString());
                avgAdd = 0;
                avgRemove = 0;
                _i = 0;
                SendCustomEventDelayedFrames(nameof(RemovePrevious), 1);
            }
        }

        public void RemovePrevious()
        {
            if (_i < _ownTransform.childCount)
            {
                var stopwatch = new System.Diagnostics.Stopwatch();
                stopwatch.Restart();
                var o = _ownTransform.GetChild(_i).gameObject;
                Assert(avlTree.Remove(o.GetComponent<ExampleSortable>()), $"Did not contain {o.name}");
                var stopwatchElapsed = stopwatch.Elapsed;
                avgRemove += stopwatchElapsed.TotalMilliseconds;
                if (_i % 100 == 0)
                {
                    Debug.Log(_i + ": avg remove = " + avgRemove / _i + " ms");
                }

                ++_i;
                SendCustomEventDelayedFrames(nameof(RemovePrevious), 1);
            }
            else
            {
                Debug.Log(_i + ": avg remove = " + avgRemove / _i + " ms");
                Debug.Log(avlTree.ToString());

                avgAdd = 0;
                avgRemove = 0;
                _i = 0;
                SendCustomEventDelayedFrames(nameof(AddNext), 1);
            }
        }
    }
}