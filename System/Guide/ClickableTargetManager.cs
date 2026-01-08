using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using UnityEngine;
using Vant.System;

namespace Van.System.Guide
{
    public class ClickableWrapper
    {
        public string key;
        public GameObject handle;
        public Action clickCallBack;

        public bool IsActiveAndClickable()
        {
            return !string.IsNullOrEmpty(key) && handle != null && handle.activeInHierarchy && IsPositionValid();
        }

        public bool IsPositionValid()
        {
            // TODO 检查超出屏幕边界
            return true;
        }
    }

    public class ClickableTargetManager
    {
        private static ClickableTargetManager _instance;
        public static ClickableTargetManager Instance => _instance ??= new ClickableTargetManager();

        private Dictionary<string, ClickableWrapper> _clickableTargets = new Dictionary<string, ClickableWrapper>();

        public static void RegisterTarget(ClickableWrapper clickable)
        {
            if (clickable == null) return;
            if (string.IsNullOrEmpty(clickable.key)) return;
            if (clickable.handle == null) return;

            Instance._clickableTargets[clickable.key] = clickable;
        }

        public static void UnregisterTarget(string key)
        {
            if (string.IsNullOrEmpty(key)) return;

            Instance._clickableTargets.Remove(key);
        }

        /// <summary>
        /// 轮询获取有效目标（支持简短轮询，防止目标刚创建尚未注册的情况）
        /// </summary>
        public static async UniTask<ClickableWrapper> GetValidTarget(string key, int maxRetries = 5, int intervalMs = 50, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(key)) return null;

            for (int i = 0; i < maxRetries; i++)
            {
                var target = Instance._clickableTargets.GetValueOrDefault(key);

                if (target != null && target.IsActiveAndClickable())
                {
                    if (target.handle.TryGetComponent<UnityEngine.UI.Button>(out var btn))
                    {
                        target.clickCallBack = () =>
                        {
                            btn.onClick?.Invoke();
                        };
                    }
                    return target;
                }

                if (i < maxRetries - 1)
                {
                    var index = await UniTask.WhenAny(TaskManager.WaitUntilCanceled(cancellationToken), UniTask.Delay(intervalMs));
                    if (index == 0) break;
                }
            }

            return null;
        }

        public static async void OverrideClickCallBack(string key, Action callback)
        {
            var target = await GetValidTarget(key);
            if (target != null)
            {
                target.clickCallBack = callback;
            }
        }
    }
}