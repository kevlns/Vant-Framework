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
        private GuideStepBase _currentStep;

        // 已完成的引导步骤 ID 集合，防止重复引导
        public HashSet<string> FinishedGuideSteps { get; private set; } = new HashSet<string>();

        // 存储所有步骤处理器的实例 (StepType -> Instance)
        private readonly Dictionary<string, GuideStepBase> _stepProcessors = new Dictionary<string, GuideStepBase>();

        #region CTS 定义

        // 引导使用的 CancellationTokenSource
        private CancellationTokenSource _stepCTS;
        private CancellationTokenSource _groupCTS;

        #endregion

        #region 事件及代理

        // 外部注入的事件，用于获取当前满足条件的引导列表
        public event Func<List<object>> GetValidGuidesEvent;

        // 外部注入的事件，用于对满足条件的引导列表进行排序 (返回排序后的列表)
        public event Func<List<object>, List<object>> SortValidGuidesEvent;

        // 外部注入的数据提取器，用于从引导数据中提取所需的固定字段 StepId 和 StepType
        public Func<object, string> StepIdExtractor { get; set; }
        public Func<object, string> StepTypeExtractor { get; set; }

        #endregion


        public GuideManager(AppCore appCore)
        {
            Instance = this;
            _appCore = appCore;
            _groupCTS?.Dispose();
            _groupCTS = TaskManager.Instance.CreateLinkedCTS();
            _stepCTS?.Dispose();
            _stepCTS = TaskManager.Instance.CreateLinkedCTS(_groupCTS.Token);
            InitializeStepProcessors();

            _appCore.Notifier.AddListener(GuideInternalEvent.EnableGuide, OnEnableGuide);
            _appCore.Notifier.AddListener(GuideInternalEvent.DisableGuide, OnDisableGuide);
            _appCore.Notifier.AddListener(GuideInternalEvent.UpdateFinishedGuideSteps, OnUpdateFinishedGuideSteps);
            _appCore.Notifier.AddListener(GuideInternalEvent.TryStartGuide, OnTryStartGuide);
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
            if (!_isGuideEnable) return;
            InternalTryStartGuide();
        }

        private async void InternalTryStartGuide()
        {
            if (StepIdExtractor == null || StepTypeExtractor == null)
            {
                Debug.LogError("[GuideManager] Extractors not set! Cannot parse guide data.");
                return;
            }

            ConditionContext.UpdateContext();
            var guideSteps = GetValidGuidesEvent?.Invoke();
            if (guideSteps == null || guideSteps.Count == 0) return;
            guideSteps = SortValidGuidesEvent?.Invoke(guideSteps) ?? guideSteps;

            // 只启动优先级最高的第一个引导
            var topGuideData = guideSteps[0];
            string stepId = StepIdExtractor(topGuideData);
            string stepType = StepTypeExtractor(topGuideData);

            // 检查是否是重复引导或者当前已有引导在进行中
            if (FinishedGuideSteps.Contains(stepId) || _currentStep != null || _currentStep.StepId == stepId) return;

            var processor = GetStepProcessor(stepType);
            if (processor == null)
            {
                Debug.LogError($"[GuideManager] No processor found for Type: {stepType} (ID: {stepId})");
                return;
            }

            try
            {
                _currentStep = processor;
                await processor.Preload();
                string result = await processor.Play(stepId, topGuideData, _stepCTS);
            }
            catch (OperationCanceledException ex)
            {
                
            }
            catch (Exception ex)
            {
                Debug.LogError($"[GuideManager] Step Error: {ex}");
            }
            finally
            {
                if (_currentStep == processor)
                {
                    _currentStep = null;
                }
                DisposeCTS();
            }
        }

        public void StopCurrentGuide()
        {
            if (_stepCTS != null)
            {
                _stepCTS.Cancel();
                DisposeCTS();
            }
            _currentStep = null;
        }

        private void DisposeCTS()
        {
            if (_stepCTS != null)
            {
                _stepCTS.Dispose();
                _stepCTS = null;
            }
        }
    }
}
