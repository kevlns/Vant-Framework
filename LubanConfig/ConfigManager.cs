using System;
using System.Threading.Tasks;
using Luban;
using UnityEngine;
using Vant.Core;

namespace Vant.LubanConfig
{
    /// <summary>
    /// 配置管理器
    /// 负责 Luban 配置表的加载与访问
    /// </summary>
    public class ConfigManager
    {
        /// <summary>
        /// 配置表实例
        /// </summary>
        public object Tables { get; private set; }

        /// <summary>
        /// 自定义加载函数（用于热更模式）
        /// 参数：文件名 (不含后缀)
        /// 返回：Task<ByteBuf>
        /// </summary>
        public Func<string, Task<ByteBuf>> CustomLoader { get; set; }

        /// <summary>
        /// 获取强类型配置表
        /// </summary>
        /// <typeparam name="T">配置表类型</typeparam>
        /// <returns></returns>
        public T GetTables<T>() where T : class
        {
            return Tables as T;
        }

        /// <summary>
        /// 加载配置表
        /// </summary>
        /// <typeparam name="T">具体的 Tables 类型</typeparam>
        /// <param name="tableCreator">创建 Tables 的委托，传入加载函数</param>
        public Task LoadAsync<T>(Func<Func<string, Task<ByteBuf>>, T> tableCreator) where T : class
        {
            try
            {
                if (!AppCore.GlobalSettings.LUBAN_HOTFIX)
                {
                    Tables = tableCreator(LoadByteBufFromResourcesAsync);
                }
                else
                {
                    if (CustomLoader == null)
                    {
                        Debug.LogError("[ConfigManager] 热更模式开启，但未设置 CustomLoader！");
                        return Task.CompletedTask;
                    }
                    Tables = tableCreator(CustomLoader);
                }
                Debug.Log("[ConfigManager] 所有配置加载成功！");
            }
            catch (global::System.Exception e)
            {
                Debug.LogError($"[ConfigManager] 加载配置时发生错误: {e.Message}");
            }
            return Task.CompletedTask;
        }

        /// <summary>
        /// 检查配置表是否已加载
        /// </summary>
        /// <typeparam name="T">配置表类型</typeparam>
        /// <returns>是否已加载</returns>
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public bool IsLoaded<T>() where T : class
        {
            return Tables is T;
        }

        private static Task<ByteBuf> LoadByteBufFromResourcesAsync(string file)
        {
            // 路径前缀，需确保文件位于 Resources/ConfigBinary/ 下
            // 去除可能的结尾 '/'，避免出现双斜杠或空段
            string prefix = AppCore.GlobalSettings.LUBAN_CONFIG_PATH_NON_HF ?? string.Empty;
            prefix = prefix.TrimEnd('/', '\\');

            string path = string.IsNullOrEmpty(prefix) ? file : $"{prefix}/{file}";
            TextAsset configFile = UnityEngine.Resources.Load<TextAsset>(path);

            if (configFile == null)
            {
                Debug.LogError($"[ConfigManager] 配置文件 {path} 未找到！");
                return Task.FromResult(new ByteBuf(new byte[0]));
            }

            // 拷贝数据
            var bytes = configFile.bytes;

            // 优化：立即卸载 TextAsset 释放内存
            UnityEngine.Resources.UnloadAsset(configFile);

            return Task.FromResult(new ByteBuf(bytes));
        }
    }
}
