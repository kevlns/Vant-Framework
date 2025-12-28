using System;
using System.Threading;
using NativeWebSocket;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Vant.Net.WebSocket
{
    /// <summary>
    /// 封装 NativeWebSocket 的客户端，处理连接、发送、接收和事件分发。
    /// </summary>
    public class WebSocketClient
    {
        private NativeWebSocket.WebSocket _websocket;
        private string _url;

        public event Action OnOpen;
        public event Action<byte[]> OnMessage;
        public event Action<string> OnError;
        public event Action<WebSocketCloseCode> OnClose;

        public WebSocketState State => _websocket?.State ?? WebSocketState.Closed;

        private CancellationTokenSource _heartbeatCts;
        /// <summary>
        /// 心跳间隔（秒），默认 10 秒
        /// </summary>
        public float HeartbeatInterval { get; set; } = 10f;
        /// <summary>
        /// 自定义发送心跳的逻辑。如果不设置，默认发送空字节数组。
        /// </summary>
        public Func<UniTask> SendHeartbeatAction { get; set; }

        /// <summary>
        /// 连接到指定的 WebSocket URL
        /// </summary>
        /// <param name="url">ws:// or wss:// url</param>
        public async UniTask ConnectAsync(string url)
        {
            _url = url;

            // 如果已有连接，先关闭
            if (_websocket != null && (_websocket.State == WebSocketState.Open || _websocket.State == WebSocketState.Connecting))
            {
                await CloseAsync();
            }

            _websocket = new NativeWebSocket.WebSocket(url);

            _websocket.OnOpen += () =>
            {
                StartHeartbeat();
                OnOpen?.Invoke();
            };
            _websocket.OnMessage += (bytes) => OnMessage?.Invoke(bytes);
            _websocket.OnError += (e) => OnError?.Invoke(e);
            _websocket.OnClose += (e) =>
            {
                StopHeartbeat();
                OnClose?.Invoke(e);
            };

            try
            {
                await _websocket.Connect();
            }
            catch (Exception e)
            {
                OnError?.Invoke(e.Message);
            }
        }

        public async UniTask<bool> ReconnectAsync()
        {
            if (_url == null)
            {
                Debug.LogError("WebSocketClient: Cannot reconnect, url is null");
                return false;
            }

            await ConnectAsync(_url);
            return true;
        }

        /// <summary>
        /// 发送二进制数据
        /// </summary>
        public void SendBytes(byte[] data)
        {
            if (_websocket != null && _websocket.State == WebSocketState.Open)
            {
                _websocket.Send(data);
            }
            else
            {
                Debug.LogWarning("WebSocketClient: Cannot send message, state is " + State);
            }
        }

        /// <summary>
        /// 发送文本数据
        /// </summary>
        public void SendText(string message)
        {
            if (_websocket != null && _websocket.State == WebSocketState.Open)
            {
                _websocket.SendText(message);
            }
            else
            {
                Debug.LogWarning("WebSocketClient: Cannot send message, state is " + State);
            }
        }

        /// <summary>
        /// 关闭连接
        /// </summary>
        public async UniTask CloseAsync()
        {
            StopHeartbeat();
            if (_websocket != null)
            {
                await _websocket.Close();
                _websocket = null;
            }
        }

        private void StartHeartbeat()
        {
            StopHeartbeat();
            _heartbeatCts = new CancellationTokenSource();
            HeartbeatLoop(_heartbeatCts.Token).Forget();
        }

        private void StopHeartbeat()
        {
            if (_heartbeatCts != null)
            {
                _heartbeatCts.Cancel();
                _heartbeatCts.Dispose();
                _heartbeatCts = null;
            }
        }

        private async UniTaskVoid HeartbeatLoop(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                await UniTask.Delay(TimeSpan.FromSeconds(HeartbeatInterval), cancellationToken: token);
                if (token.IsCancellationRequested) break;

                if (_websocket != null && _websocket.State == WebSocketState.Open)
                {
                    if (SendHeartbeatAction != null)
                    {
                        await SendHeartbeatAction.Invoke();
                    }
                    else
                    {
                        // Default heartbeat: send empty byte array
                        await _websocket.SendText("ping");
                    }
                }
            }
        }

        /// <summary>
        /// 需要在主线程 Update 中调用，用于分发消息队列（非 WebGL 平台）
        /// </summary>
        public void Update()
        {
#if !UNITY_WEBGL || UNITY_EDITOR
            _websocket?.DispatchMessageQueue();
#endif
        }
    }
}
