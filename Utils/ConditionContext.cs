using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Vant.Utils
{
    public static class ConditionContext
    {
        public static event Action OnUpdateCtxEvent;
        private static Dictionary<string, object> _forceCtx = new Dictionary<string, object>();
        private static Dictionary<string, object> _tempCtx = new Dictionary<string, object>();

        public static void SetForceData(string key, object value)
        {
            _forceCtx[key] = value;
        }

        public static void SetTempData(string key, object value)
        {
            _tempCtx[key] = value;
        }

        public static object GetData(string key)
        {
            if (_forceCtx.TryGetValue(key, out var value))
            {
                return value;
            }
            if (_tempCtx.TryGetValue(key, out value))
            {
                return value;
            }
            return null;
        }

        public static void UpdateContext()
        {
            if (OnUpdateCtxEvent == null) return;

            _forceCtx.Clear();
            Delegate[] invocationList = OnUpdateCtxEvent.GetInvocationList();
            foreach (var handler in invocationList)
            {
                try
                {
                    ((Action)handler).Invoke();
                }
                catch (Exception ex)
                {
                    UnityEngine.Debug.LogError($"[ConditionContext] Update Event Error: {ex}");
                }
            }
        }
    }
}