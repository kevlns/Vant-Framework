using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;
using Newtonsoft.Json;

namespace Vant.Net.Http
{
    /// <summary>
    /// 通用的 HTTP 客户端核心
    /// 负责处理 UnityWebRequest 的生命周期
    /// </summary>
    public class HttpClient
    {
        // 全局 Headers (如 Token)
        private readonly Dictionary<string, string> _globalHeaders = new Dictionary<string, string>();

        // 全局 JSON 序列化设置
        private readonly JsonSerializerSettings _jsonSettings = new JsonSerializerSettings
        {
            NullValueHandling = NullValueHandling.Ignore
        };

        public void SetGlobalHeader(string key, string value)
        {
            if (_globalHeaders.ContainsKey(key))
                _globalHeaders[key] = value;
            else
                _globalHeaders.Add(key, value);
        }

        public void RemoveGlobalHeader(string key)
        {
            if (_globalHeaders.ContainsKey(key))
                _globalHeaders.Remove(key);
        }

        /// <summary>
        /// 发送请求并获取原始字符串响应
        /// </summary>
        public void Send(HttpRequest request, Action<string> onSuccess, Action<string> onError)
        {
            SendAsync(request).ContinueWith(onSuccess).Forget(e => onError?.Invoke(e.Message));
        }

        /// <summary>
        /// 发送请求并自动反序列化为对象 T
        /// </summary>
        public void Send<T>(HttpRequest request, Action<T> onSuccess, Action<string> onError)
        {
            SendAsync<T>(request).ContinueWith(onSuccess).Forget(e => onError?.Invoke(e.Message));
        }

        public async UniTask<T> SendAsync<T>(HttpRequest request)
        {
            string json = await SendAsync(request);
            try
            {
                return JsonConvert.DeserializeObject<T>(json, _jsonSettings);
            }
            catch (Exception e)
            {
                Debug.LogError($"[HttpClient] JSON Deserialization Failed: {e.Message}\nJSON: {json}");
                throw;
            }
        }

        public async UniTask<string> SendAsync(HttpRequest request)
        {
            string finalUrl = request.BuildUrl();
            string method = request.Method.ToString();

            using (UnityWebRequest webRequest = new UnityWebRequest(finalUrl, method))
            {
                // 1. 设置 Body
                if (request.Body != null)
                {
                    // 使用 Newtonsoft.Json 替代 JsonUtility
                    string jsonBody = JsonConvert.SerializeObject(request.Body, _jsonSettings);
                    byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonBody);
                    webRequest.uploadHandler = new UploadHandlerRaw(bodyRaw);
                }

                webRequest.downloadHandler = new DownloadHandlerBuffer();

                // // 2. 设置 Headers
                // // 默认 Header
                // webRequest.SetRequestHeader("Content-Type", "application/json");
                // webRequest.SetRequestHeader("Access-Control-Allow-Origin", "*");

                // 全局 Header (如 Token)
                foreach (var header in _globalHeaders)
                {
                    webRequest.SetRequestHeader(header.Key, header.Value);
                }

                // 请求特定 Header
                foreach (var header in request.Headers)
                {
                    webRequest.SetRequestHeader(header.Key, header.Value);
                }

                // 3. 发送
                try
                {
                    await UnityAsyncExtensions.ToUniTask(webRequest.SendWebRequest());

                    // 4. 处理结果
                    if (webRequest.result == UnityWebRequest.Result.Success)
                    {
                        string text = webRequest.downloadHandler.text;
                        Debug.Log($"[HttpClient] {method} {finalUrl} Success: {text}");
                        return text;
                    }
                    else
                    {
                        string errorMsg = $"[HttpClient] {method} {finalUrl} Failed: {webRequest.error}\nResponse: {webRequest.downloadHandler.text}";
                        Debug.LogError(errorMsg);
                        throw new Exception(errorMsg);
                    }
                }
                catch (UnityWebRequestException e)
                {
                    string errorMsg = $"[HttpClient] {method} {finalUrl} Exception: {e.Message}";
                    Debug.LogError(errorMsg);
                    throw;
                }
            }
        }
    }
}
