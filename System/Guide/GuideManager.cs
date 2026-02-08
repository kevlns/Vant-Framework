using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using Vant.Core;
using Vant.Utils;

namespace Vant.System.Guide
{
    /// <summary>
    /// 引导系统管理器
    /// </summary>
    public class GuideManager
    {
        public static GuideManager Instance { get; private set; }

        // appCore 引用
        private AppCore _appCore;

        // 引导系统启用标志位
        private bool _isGuideEnable = false;

        // 当前正在进行的步骤
        private string _currentGroupId;
        private GuideStepBase _currentStep;

        // 已完成的引导步骤 ID 集合，防止重复引导
        public HashSet<string> FinishedGuideSteps { get; private set; } = new HashSet<string>();

        // 存储所有步骤处理器的实例 (StepType -> Instance)
        private readonly Dictionary<string, GuideStepBase> _stepProcessors = new Dictionary<string, GuideStepBase>();
        private TaskChain _guideTaskChain;

        #region CTS 定义

        // 引导使用的 CancellationTokenSource
        private CancellationTokenSource _stepCTS;
        private CancellationTokenSource _groupCTS;

        #endregion

        #region 事件及代理

        private DataExtractors _dataCore;
        public class DataExtractors
        {
            public Func<List<object>> GetValidGuideGroups;  // 获取满足条件的引导组列表
            public Func<object, List<object>> GetGuideGroupSteps;  // 获取指定引导组的所有步骤列表
            public Func<List<object>, List<object>> SortGuideGroups; // 对引导组进行自定义排序，如果不传入则按默认顺序
            public Func<List<object>, List<object>> SortGuideSteps; // 对引导步骤进行自定义排序
            public Func<object, string> GroupIdExtractor { get; set; }  // 组id提取器
            public Func<object, string> StepIdExtractor { get; set; }  // 步骤id提取器
            public Func<object, string> StepTypeExtractor { get; set; }  // 步骤类型提取器
        }

        #endregion


        public GuideManager(AppCore appCore)
        {
            Instance = this;
            _appCore = appCore;
            _guideTaskChain = TaskManager.Instance.CreateChain(needFuse: true);
            InitializeStepProcessors();

            _appCore.Notifier.AddListener(GuideInternalEvent.EnableGuide, OnEnableGuide);
            _appCore.Notifier.AddListener(GuideInternalEvent.DisableGuide, OnDisableGuide);
            _appCore.Notifier.AddListener(GuideInternalEvent.UpdateFinishedGuideSteps, OnUpdateFinishedGuideSteps);
            _appCore.Notifier.AddListener(GuideInternalEvent.TryStartGuide, OnTryStartGuide);
            _appCore.Notifier.AddListener(GuideInternalEvent.StopCurrentGuideGroup, OnStopCurrentGuideGroup);
        }

        public void SetupExtractors(DataExtractors dataExtractors)
        {
            _dataCore = dataExtractors;
        }

        private void InitializeStepProcessors()
        {
            _stepProcessors.Clear();
            var targetType = typeof(GuideStepBase);

            // 获取当前域中所有程序集
            var assemblies = AppDomain.CurrentDomain.GetAssemblies();
            foreach (var assembly in assemblies)
            {
                // 简单的过滤，根据需要可以调整过滤规则
                if (assembly.IsDynamic ||
                    assembly.FullName.StartsWith("Unity") ||
                    assembly.FullName.StartsWith("System") ||
                    assembly.FullName.StartsWith("mscorlib"))
                    continue;

                Type[] types;
                try
                {
                    types = assembly.GetTypes();
                }
                catch (ReflectionTypeLoadException ex)
                {
                    types = ex.Types.Where(t => t != null).ToArray();
                }
                catch (Exception)
                {
                    continue;
                }

                foreach (var type in types)
                {
                    if (type.IsClass && !type.IsAbstract && type.IsSubclassOf(targetType))
                    {
                        try
                        {
                            // 创建实例
                            if (Activator.CreateInstance(type) is GuideStepBase stepInstance)
                            {
                                // 过滤掉 Type 为 None 的
                                if (stepInstance.StepType != GuideStepType.None)
                                {
                                    if (!_stepProcessors.ContainsKey(stepInstance.StepType))
                                    {
                                        _stepProcessors.Add(stepInstance.StepType, stepInstance);
                                    }
                                    else
                                    {
                                        Debug.LogError($"[GuideManager] Duplicate StepType: {stepInstance.StepType}. Class: {type.Name}, Existing: {_stepProcessors[stepInstance.StepType].GetType().Name}");
                                    }
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            Debug.LogError($"[GuideManager] Failed to create instance of {type.Name}: {ex}");
                        }
                    }
                }
            }
        }

        /// <summary>
        /// 获取指定类型的步骤处理器实例
        /// </summary>
        private GuideStepBase GetStepProcessor(string stepType)
        {
            if (_stepProcessors.TryGetValue(stepType, out var processor))
            {
                return processor;
            }
            return null;
        }

        private void OnEnableGuide(object data)
        {
            _isGuideEnable = true;
        }

        private void OnDisableGuide(object data)
        {
            _isGuideEnable = false;
        }

        private void OnUpdateFinishedGuideSteps(object data)
        {
            if (data is IEnumerable<string> finishedSteps)
            {
                FinishedGuideSteps.Clear();
                foreach (var step in finishedSteps)
                {
                    FinishedGuideSteps.Add(step);
                }
            }
        }

        public void OnTryStartGuide(object data)
        {
            if (!_isGuideEnable)
            {
                Debug.Log("[GuideManager] Guide system is disabled.");
                return;
            }

            if (_dataCore == null)
            {
                Debug.LogError("[GuideManager] Data extractors for getting guide groups or steps are not set.");
                return;
            }

            if (_currentStep != null)
            {
                Debug.Log("[GuideManager] A guide step is already in progress.");
                return;
            }

            InternalTryStartGuide();
        }

        private void OnStopCurrentGuideGroup(object data)
        {
            if (_groupCTS != null)
            {
                _groupCTS.Cancel();
            }
            DisposeAllCTS();
            _currentStep = null;
        }

        private void InternalTryStartGuide()
        {
            ConditionContext.UpdateContext();
            var validGroups = _dataCore.GetValidGuideGroups?.Invoke();
            if (validGroups == null || validGroups.Count == 0) return;
            validGroups = _dataCore.SortGuideGroups?.Invoke(validGroups) ?? validGroups;

            var topGroupData = validGroups[0];
            var steps = _dataCore.GetGuideGroupSteps?.Invoke(topGroupData);
            steps = _dataCore.SortGuideSteps?.Invoke(steps) ?? steps;

            _groupCTS?.Dispose();
            _groupCTS = TaskManager.Instance.CreateLinkedCTS();
            _stepCTS?.Dispose();
            _stepCTS = TaskManager.Instance.CreateLinkedCTS(_groupCTS.Token);
            try
            {
                foreach (var stepData in steps)
                {
                    _guideTaskChain.EnqueueAsync(() => GuideTask(topGroupData, stepData, _stepCTS.Token));
                }
            }
            catch (OperationCanceledException)
            {
                Debug.Log("[GuideManager] Guide group execution was canceled.");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[GuideManager] Exception during guide group execution: {ex}");
            }
            finally
            {
            }
        }

        private async UniTask GuideTask(object groupData, object stepData, CancellationToken token)
        {
            _currentGroupId = _dataCore.GroupIdExtractor?.Invoke(groupData);
            var stepId = _dataCore.StepIdExtractor?.Invoke(stepData);
            if (_currentGroupId == null || stepId == null)
            {
                _currentGroupId = null;
                _currentStep = null;
                Debug.LogError("[GuideManager] GroupId or StepId is null.");
                return;
            }

            var stepType = _dataCore.StepTypeExtractor?.Invoke(stepData);
            _currentStep = GetStepProcessor(stepType);
            if (_currentStep == null)
            {
                _currentGroupId = null;
                _currentStep = null;
                Debug.LogError($"[GuideManager] No step processor found for StepType: {stepType}, StepId: {stepId}");
                return;
            }

            try
            {
                // TODO 任务执行
                await UniTask.CompletedTask;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[GuideManager] Exception during Preload of StepId: {stepId}, Exception: {ex}");
            }
        }

        private void DisposeAllCTS()
        {
            _stepCTS?.Dispose();
            _stepCTS = null;
            _groupCTS?.Dispose();
            _groupCTS = null;
        }
    }
}
