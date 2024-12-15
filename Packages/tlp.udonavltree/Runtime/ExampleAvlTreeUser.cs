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

        public AvlTree AvlTree;

        private Transform _ownTransform;

        public int Count = 10000;

        #region State
        private int _i;
        private double _avgAdd;
        private double _avgRemove;
        private int _instantiated;
        #endregion


        #region Base Overrides
        protected override bool SetupAndValidate() {
            if (!base.SetupAndValidate()) {
                return false;
            }

            _ownTransform = transform;
            return TaskScheduler.AddTaskToDefaultScheduler(this, this);
        }

        public override void OnEvent(string eventName) {
            switch (eventName) {
                case "OnTaskFinished":

                    #region TLP_DEBUG
#if TLP_DEBUG
                    DebugLog_OnEvent(eventName);
#endif
                    #endregion

                    break;
                default:
                    base.OnEvent(eventName);
                    break;
            }
        }
        #endregion

        #region Task Implementation
        protected override TaskResult RunStep() {
            var firstChild = _ownTransform.GetChild(0).gameObject.GetComponent<ExampleSortable>();
            if (_instantiated >= Count) {
                Destroy(firstChild.gameObject);
                SendCustomEventDelayedSeconds(nameof(AddNext), 1);
                return TaskResult.Succeeded;
            }

            var go = Instantiate(firstChild.gameObject, _ownTransform);
            go.name = _instantiated.ToString();
            go.GetComponent<ExampleSortable>().value = _instantiated;

            _instantiated++;

            return TaskResult.Unknown;
        }

        protected override bool InitTask() {
            // nothing to do
            return true;
        }
        #endregion

        #region Benchmarking
        public void AddNext() {
            if (!HasStartedOk) {
                Error($"{nameof(AddNext)}: Not initialized");
                return;
            }

            if (_i < _ownTransform.childCount) {
                var stopwatch = new Stopwatch();
                var o = _ownTransform.GetChild(_i).gameObject;
                var tlpBaseBehaviour = o.GetComponent<ExampleSortable>();
                stopwatch.Restart();
                if (!AvlTree.Add(tlpBaseBehaviour)) {
                    ErrorAndDisableGameObject($"Failed to add {o.name}");
                    return;
                }

                var stopwatchElapsed = stopwatch.Elapsed;
                _avgAdd += stopwatchElapsed.TotalMilliseconds;
                if (_i % 100 == 0) {
                    Debug.Log(_i + ": avg add = " + _avgAdd / _i + " ms");
                }

                ++_i;
                SendCustomEventDelayedFrames(nameof(AddNext), 1);
            } else {
                Debug.Log(_i + ": avg add = " + _avgAdd / _i + " ms");
                Debug.Log(AvlTree.ToString());
                if (AvlTree.Size > 0) {
                    Info(AvlTree.Display(AvlTree.RootNode).ToString());
                }

                _avgAdd = 0;
                _avgRemove = 0;
                _i = 0;
                SendCustomEventDelayedFrames(nameof(RemovePrevious), 1);
            }
        }

        public void RemovePrevious() {
            if (_i < _ownTransform.childCount) {
                var stopwatch = new Stopwatch();
                var o = _ownTransform.GetChild(_i).gameObject;
                var udonSharpBehaviour = o.GetComponent<ExampleSortable>();

                stopwatch.Restart();
                if (!AvlTree.Remove(udonSharpBehaviour)) {
                    ErrorAndDisableGameObject($"Did not contain {o.name}");
                    return;
                }

                var stopwatchElapsed = stopwatch.Elapsed;
                _avgRemove += stopwatchElapsed.TotalMilliseconds;
                if (_i % 100 == 0) {
                    Debug.Log(_i + ": avg remove = " + _avgRemove / _i + " ms");
                }

                ++_i;
                SendCustomEventDelayedFrames(nameof(RemovePrevious), 1);
            } else {
                Debug.Log(_i + ": avg remove = " + _avgRemove / _i + " ms");
                Debug.Log(AvlTree.ToString());
                if (AvlTree.Size > 0) {
                    Info(AvlTree.Display(AvlTree.RootNode).ToString());
                }

                _avgAdd = 0;
                _avgRemove = 0;
                _i = 0;
                SendCustomEventDelayedFrames(nameof(AddNext), 1);
            }
        }
        #endregion
    }
}