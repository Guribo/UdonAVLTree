using System.Diagnostics;
using JetBrains.Annotations;
using TLP.UdonUtils.Runtime.Experimental.Tasks;
using UdonSharp;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace TLP.UdonAVLTree.Runtime
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    [DefaultExecutionOrder(ExecutionOrder)]
    [TlpDefaultExecutionOrder(typeof(ExampleAvlTreeUser), ExecutionOrder)]
    public class ExampleAvlTreeUser : Task
    {
        #region ExecutionOrder
        public override int ExecutionOrderReadOnly => ExecutionOrder;


        [PublicAPI]
        public new const int ExecutionOrder = AvlTree.ExecutionOrder + 1;
        #endregion

        #region Dependencies
        public AvlTree AvlTree;
        #endregion

        #region State
        private Transform _ownTransform;
        public int Count = 10000;
        #endregion

        #region State
        private int _i;
        private double _avgAdd;
        private double _avgRemove;
        private int _instantiated;
        #endregion

        #region Base Overrides
        protected override bool _SetupAndValidate() {
            if (!base._SetupAndValidate()) {
                return false;
            }

            _ownTransform = transform;
            return TaskScheduler._AddTaskToDefaultScheduler(this, this);
        }
        #endregion

        #region Task Implementation
        protected override TaskResult _DoTask(float stepDeltaTime) {
            var firstChild = _ownTransform.GetChild(0).gameObject.GetComponent<ExampleSortable>();
            if (_instantiated >= Count) {
                Destroy(firstChild.gameObject);
                SendCustomEventDelayedSeconds(nameof(_AddNext), 1);
                return TaskResult.Succeeded;
            }

            var go = Instantiate(firstChild.gameObject, _ownTransform);
            go.name = _instantiated.ToString();
            go.GetComponent<ExampleSortable>().Value = _instantiated;

            _instantiated++;

            return TaskResult.Unknown;
        }

        public override int _GetNeededSteps() {
            return Count;
        }

        protected override bool _InitTask() {
            // nothing to do
            return true;
        }

        protected override void _OnTaskFinished() {
            #region TLP_DEBUG
#if TLP_DEBUG
            _DebugLog_OnEvent(nameof(_OnTaskFinished));
#endif
            #endregion
        }
        #endregion

        #region Benchmarking
        public void _AddNext() {
            if (!HasStartedOk) {
                _Error($"{nameof(_AddNext)}: Not initialized");
                return;
            }

            if (_i < _ownTransform.childCount) {
                var stopwatch = new Stopwatch();
                var o = _ownTransform.GetChild(_i).gameObject;
                var tlpBaseBehaviour = o.GetComponent<ExampleSortable>();
                stopwatch.Restart();
                if (!AvlTree._Add(tlpBaseBehaviour)) {
                    _ErrorAndDisableGameObject($"Failed to add {o.name}");
                    return;
                }

                var stopwatchElapsed = stopwatch.Elapsed;
                _avgAdd += stopwatchElapsed.TotalMilliseconds;
                if (_i % 100 == 0) {
                    Debug.Log(_i + ": avg add = " + _avgAdd / _i + " ms");
                }

                ++_i;
                SendCustomEventDelayedFrames(nameof(_AddNext), 1);
            } else {
                Debug.Log(_i + ": avg add = " + _avgAdd / _i + " ms");
                Debug.Log(AvlTree.ToString());
                if (AvlTree.Size > 0) {
                    _Info(AvlTree._Display(AvlTree.RootNode).ToString());
                }

                _avgAdd = 0;
                _avgRemove = 0;
                _i = 0;
                SendCustomEventDelayedFrames(nameof(_RemovePrevious), 1);
            }
        }

        public void _RemovePrevious() {
            if (_i < _ownTransform.childCount) {
                var stopwatch = new Stopwatch();
                var o = _ownTransform.GetChild(_i).gameObject;
                var udonSharpBehaviour = o.GetComponent<ExampleSortable>();

                stopwatch.Restart();
                if (!AvlTree._Remove(udonSharpBehaviour)) {
                    _ErrorAndDisableGameObject($"Did not contain {o.name}");
                    return;
                }

                var stopwatchElapsed = stopwatch.Elapsed;
                _avgRemove += stopwatchElapsed.TotalMilliseconds;
                if (_i % 100 == 0) {
                    Debug.Log(_i + ": avg remove = " + _avgRemove / _i + " ms");
                }

                ++_i;
                SendCustomEventDelayedFrames(nameof(_RemovePrevious), 1);
            } else {
                Debug.Log(_i + ": avg remove = " + _avgRemove / _i + " ms");
                Debug.Log(AvlTree.ToString());
                if (AvlTree.Size > 0) {
                    _Info(AvlTree._Display(AvlTree.RootNode).ToString());
                }

                _avgAdd = 0;
                _avgRemove = 0;
                _i = 0;
                SendCustomEventDelayedFrames(nameof(_AddNext), 1);
            }
        }
        #endregion
    }
}