using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace Vant.System.GM
{
    /// <summary>
    /// GM 命令管理器
    /// </summary>
    public class GMManager
    {
        public class CommandInfo
        {
            public string Name;
            public string Description;
            public Action<string[]> Handler;
        }

        private readonly Dictionary<string, CommandInfo> _commands = new Dictionary<string, CommandInfo>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// 获取所有已注册的命令
        /// </summary>
        public IReadOnlyCollection<CommandInfo> AllCommands => _commands.Values;

        /// <summary>
        /// 注册命令
        /// </summary>
        public void Register(string name, string description, Action<string[]> handler)
        {
            if (_commands.ContainsKey(name))
            {
                Debug.LogWarning($"[GMManager] Command '{name}' already registered. Overwriting.");
            }

            _commands[name] = new CommandInfo
            {
                Name = name,
                Description = description,
                Handler = handler
            };
        }

        /// <summary>
        /// 注销命令
        /// </summary>
        public void Unregister(string name)
        {
            if (_commands.ContainsKey(name))
            {
                _commands.Remove(name);
            }
        }

        /// <summary>
        /// 执行命令
        /// </summary>
        /// <param name="input">完整输入字符串 (如 "add_item 1001 5")</param>
        /// <returns>执行结果信息</returns>
        public string Execute(string input)
        {
            if (string.IsNullOrWhiteSpace(input)) return "Empty input.";

            // 简单解析：按空格分割，支持引号包裹的参数需要更复杂的解析，这里暂用简单分割
            string[] parts = input.Trim().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0) return "Empty command.";

            string cmdName = parts[0];
            string[] args = parts.Skip(1).ToArray();

            if (_commands.TryGetValue(cmdName, out var cmd))
            {
                try
                {
                    cmd.Handler?.Invoke(args);
                    return $"[GM] Executed: {cmdName}";
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[GMManager] Error executing '{cmdName}': {ex}");
                    return $"[GM] Error: {ex.Message}";
                }
            }
            else
            {
                return $"[GM] Unknown command: {cmdName}";
            }
        }

        /// <summary>
        /// 自动扫描并注册带有 [GMCommand] 特性的静态方法
        /// </summary>
        public void ScanAndRegisterStaticMethods(Assembly assembly)
        {
            var methods = assembly.GetTypes()
                .SelectMany(t => t.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic))
                .Where(m => m.GetCustomAttribute<GMCommandAttribute>() != null);

            foreach (var method in methods)
            {
                var attr = method.GetCustomAttribute<GMCommandAttribute>();
                
                // 检查签名是否匹配 Action<string[]>
                var parameters = method.GetParameters();
                if (parameters.Length == 1 && parameters[0].ParameterType == typeof(string[]))
                {
                    Action<string[]> handler = (Action<string[]>)Delegate.CreateDelegate(typeof(Action<string[]>), method);
                    Register(attr.Name, attr.Description, handler);
                }
                else
                {
                    // 也可以支持无参方法
                    if (parameters.Length == 0)
                    {
                        Action<string[]> handler = _ => method.Invoke(null, null);
                        Register(attr.Name, attr.Description, handler);
                    }
                    else
                    {
                        Debug.LogWarning($"[GMManager] Method {method.Name} has invalid signature for GMCommand. Expected static void Method(string[] args) or static void Method().");
                    }
                }
            }
        }
    }
}
