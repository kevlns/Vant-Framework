using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using Vant.Core;

namespace Vant.System.Guide
{
    /// <summary>
    /// 引导系统管理器
    /// </summary>
    public class GuideManager
    {
        private static GuideManager _instance;
        public static GuideManager Instance => _instance ??= new GuideManager();

        private AppCore AppCore => AppCore.Instance;

        // 当前正在进行的引导 ID
        public int CurrentGuideId { get; private set; } = 0;

        // 当前正在进行的步骤
        private AbstractGuideStep _currentStep;
        
        // 引导配置 (GuideId -> List<Step>)
        // 实际项目中通常会从配置表读取，这里仅作演示容器
        private Dictionary<int, List<AbstractGuideStep>> _guideConfigs = new Dictionary<int, List<AbstractGuideStep>>();

        // 步骤索引
        private int _currentStepIndex = -1;

        public void Initialize()
        {
            // 初始化逻辑，例如加载配置
            Debug.Log("[GuideManager] Initialized");
        }

        public void Update(float deltaTime)
        {
            if (_currentStep != null && !_currentStep.IsCompleted)
            {
                _currentStep.Update(deltaTime);
            }
        }

        /// <summary>
        /// 注册引导配置 (通常由配置表加载器调用)
        /// </summary>
        public void RegisterGuide(int guideId, List<AbstractGuideStep> steps)
        {
            if (_guideConfigs.ContainsKey(guideId))
            {
                _guideConfigs[guideId] = steps;
            }
            else
            {
                _guideConfigs.Add(guideId, steps);
            }
        }

        /// <summary>
        /// 开始引导
        /// </summary>
        public async void StartGuide(int guideId)
        {
            if (!_guideConfigs.ContainsKey(guideId))
            {
                Debug.LogError($"[GuideManager] Guide {guideId} not found!");
                return;
            }

            // 如果有正在进行的引导，先强制结束
            if (CurrentGuideId != -1)
            {
                await StopGuide();
            }

            CurrentGuideId = guideId;
            _currentStepIndex = -1;
            
            Debug.Log($"[GuideManager] Start Guide: {guideId}");
            
            // 开始第一步
            await NextStep();
        }

        /// <summary>
        /// 停止当前引导
        /// </summary>
        public async UniTask StopGuide()
        {
            if (_currentStep != null)
            {
                await _currentStep.Exit();
                _currentStep = null;
            }

            CurrentGuideId = -1;
            _currentStepIndex = -1;
            Debug.Log("[GuideManager] Guide Stopped");
        }

        /// <summary>
        /// 执行下一步
        /// </summary>
        public async UniTask NextStep()
        {
            // 退出上一步
            if (_currentStep != null)
            {
                await _currentStep.Exit();
                _currentStep = null;
            }

            if (CurrentGuideId == -1 || !_guideConfigs.ContainsKey(CurrentGuideId))
                return;

            var steps = _guideConfigs[CurrentGuideId];
            _currentStepIndex++;

            // 检查是否所有步骤已完成
            if (_currentStepIndex >= steps.Count)
            {
                await CompleteGuide();
                return;
            }

            // 进入下一步
            _currentStep = steps[_currentStepIndex];
            _currentStep.StepId = _currentStepIndex;
            _currentStep.GuideId = CurrentGuideId;
            
            Debug.Log($"[GuideManager] Enter Step: {_currentStepIndex} (Type: {_currentStep.GetType().Name})");
            await _currentStep.Enter();
        }

        /// <summary>
        /// 完成整个引导
        /// </summary>
        private async UniTask CompleteGuide()
        {
            Debug.Log($"[GuideManager] Guide {CurrentGuideId} Completed!");
            CurrentGuideId = -1;
            _currentStep = null;
            await UniTask.CompletedTask;
            
            // 这里可以发送引导完成的事件
            // AppCore.EventManager.Trigger(GuideEvent.GuideFinished, ...);
        }

        /// <summary>
        /// 外部触发完成当前步骤 (例如点击了某个按钮)
        /// </summary>
        public async void CompleteCurrentStep()
        {
            if (_currentStep != null && !_currentStep.IsCompleted)
            {
                // 标记步骤内部完成逻辑
                // 注意：AbstractGuideStep.Complete 是 protected，
                // 实际逻辑中可能是 Step 监听事件自己调用 Complete，
                // 或者 Manager 强制切换。
                // 这里我们假设 Manager 控制流转，直接进入下一步
                await NextStep();
            }
        }
    }
}
