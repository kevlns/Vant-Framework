using System;
using UnityEngine;
using Cysharp.Threading.Tasks;
using Vant.Net.Http;
using Vant.Net.WebSocket;
using Vant.Utils;
using Vant.Core;

namespace Vant.Net
{
    /// <summary>
    /// 网络管理器，负责管理 HTTP 和 WebSocket 客户端
    /// </summary>
    public class NetManager
    {
        private AppCore _appCore;

        // WebSocket 客户端 (保留原有逻辑)
        public WebSocketClient WebSocketClient { get; private set; }

        // HTTP 客户端 (新增)
        public HttpClient HttpClient { get; private set; }

        public NetManager(AppCore appCore)
        {
            _appCore = appCore;
            _appCore.GameLifeCycle.OnUpdateEvent += OnUpdate;
            _appCore.GameLifeCycle.OnApplicationQuitEvent += OnApplicationQuit;
            _appCore.GameLifeCycle.OnDestroyEvent += OnDestroy;

            // 初始化 WebSocket
            WebSocketClient = new WebSocketClient();
            // 初始化 HTTP
            HttpClient = new HttpClient();
        }

        public void OnUpdate(float deltaTime, float unscaledDeltaTime)
        {
            WebSocketClient?.Update();
        }

        public void OnDestroy()
        {
            CloseWebSocket();
        }

        public void OnApplicationQuit()
        {
            CloseWebSocket();
        }

        private void CloseWebSocket()
        {
            if (WebSocketClient != null && WebSocketClient.State == NativeWebSocket.WebSocketState.Open)
            {
                WebSocketClient.CloseAsync().Forget();
            }
        }

        /// <summary>
        /// 便捷方法：连接 WebSocket
        /// </summary>
        public async UniTask ConnectWebSocket(string url)
        {
            await WebSocketClient.ConnectAsync(url);
        }

        /// <summary>
        /// 便捷方法：断开 WebSocket 连接
        /// </summary>
        public async UniTask DisconnectWebSocket()
        {
            await WebSocketClient.CloseAsync();
        }
    }
}
