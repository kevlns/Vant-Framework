using System;
using Cysharp.Threading.Tasks;
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
        /// 返回：ByteBuf
        /// </summary>
        public Func<string, UniTask<ByteBuf>> CustomLoaderAsync { get; set; }

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
        /// 加载配置表（同步构造，用于非热更或已准备好同步加载函数）
        /// </summary>
        /// <typeparam name="T">具体的 Tables 类型</typeparam>
        /// <param name="tableCreator">创建 Tables 的委托，传入同步加载函数</param>
        public void Load<T>(Func<Func<string, ByteBuf>, T> tableCreator) where T : class
        {
            try
            {
                if (!AppCore.GlobalSettings.LUBAN_HOTFIX)
                {
                    // 非热更模式：直接使用同步的 Resources.Load
                    Tables = tableCreator(LoadByteBufFromResources);
                }
                else
                {
                    Debug.LogWarning("[ConfigManager] 热更模式建议使用异步加载入口 LoadAsync(Func<UniTask<T>>)。");
                    Tables = tableCreator(LoadByteBufFromResources);
                }
                Debug.Log("[ConfigManager] 所有配置加载成功！");
            }
            catch (Exception e)
            {
                Debug.LogError($"[ConfigManager] 加载配置时发生错误: {e.Message}\n{e.StackTrace}");
            }
        }

        /// <summary>
        /// 加载配置表（异步构造，用于热更模式）
        /// </summary>
        /// <typeparam name="T">具体的 Tables 类型</typeparam>
        /// <param name="tableCreatorAsync">异步创建 Tables 的委托</param>
        public async UniTask LoadAsync<T>(Func<UniTask<T>> tableCreatorAsync) where T : class
        {
            try
            {
                Tables = await tableCreatorAsync();
                Debug.Log("[ConfigManager] 所有配置加载成功！");
            }
            catch (Exception e)
            {
                Debug.LogError($"[ConfigManager] 加载配置时发生错误: {e.Message}\n{e.StackTrace}");
            }
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

        /// <summary>
        /// 重置Tables
        /// </summary>
        public void Reset()
        {
            Tables = null;
        }

        private static ByteBuf LoadByteBufFromResources(string file)
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
                return new ByteBuf(new byte[0]);
            }

            // 拷贝数据并卸载资源
            var bytes = configFile.bytes;
            UnityEngine.Resources.UnloadAsset(configFile);
            return new ByteBuf(bytes);
        }
    }
}
