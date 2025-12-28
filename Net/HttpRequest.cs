using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Vant.Net.Http
{
    /// <summary>
    /// HTTP 请求方法枚举
    /// </summary>
    public enum HttpMethod
    {
        GET,
        POST,
        PUT,
        DELETE,
        HEAD,
        PATCH
    }

    /// <summary>
    /// HTTP 请求构建器
    /// 用于封装一次请求的所有参数
    /// </summary>
    public class HttpRequest
    {
        public string BaseUrl { get; private set; }
        public HttpMethod Method { get; private set; } = HttpMethod.GET;
        
        public object Body { get; private set; }
        public Dictionary<string, string> PathParams { get; } = new Dictionary<string, string>();
        public Dictionary<string, string> QueryParams { get; } = new Dictionary<string, string>();
        public Dictionary<string, string> Headers { get; } = new Dictionary<string, string>();

        public HttpRequest(string baseUrl, HttpMethod method)
        {
            BaseUrl = baseUrl;
            Method = method;
        }

        /// <summary>
        /// 设置路径参数，例如 /users/{id}，调用 SetPathParam("id", "1") 后变为 /users/1
        /// </summary>
        public HttpRequest SetPathParam(string key, object value)
        {
            if (value != null)
            {
                PathParams[key] = value.ToString();
            }
            return this;
        }

        /// <summary>
        /// 设置查询参数，例如 ?page=1&size=10
        /// </summary>
        public HttpRequest SetQueryParam(string key, object value)
        {
            if (value != null)
            {
                QueryParams[key] = value.ToString();
            }
            return this;
        }

        /// <summary>
        /// 设置 Body 数据 (将被序列化为 JSON)
        /// </summary>
        public HttpRequest SetBody(object body)
        {
            Body = body;
            return this;
        }

        public HttpRequest AddHeader(string key, string value)
        {
            Headers[key] = value;
            return this;
        }

        /// <summary>
        /// 构建最终 URL
        /// </summary>
        public string BuildUrl()
        {
            StringBuilder sb = new StringBuilder(BaseUrl);

            // 1. 替换路径参数
            foreach (var kvp in PathParams)
            {
                sb.Replace($"{{{kvp.Key}}}", kvp.Value);
            }

            // 2. 拼接查询参数
            if (QueryParams.Count > 0)
            {
                sb.Append("?");
                List<string> queryParts = new List<string>();
                foreach (var kvp in QueryParams)
                {
                    queryParts.Add($"{kvp.Key}={kvp.Value}");
                }
                sb.Append(string.Join("&", queryParts));
            }

            return sb.ToString();
        }
    }
}
