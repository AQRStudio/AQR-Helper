# OAuth2 游戏集成指南（Unity C#）

本文档为基于Unity（C#）的桌面游戏开发者提供完整的OAuth2授权登录集成指南。

## 目录

1. [概述](#概述)
2. [前置准备](#前置准备)
3. [OAuth2授权流程](#oauth2授权流程)
4. [API端点详细说明](#api端点详细说明)
5. [集成示例](#集成示例)
6. [错误处理](#错误处理)
7. [安全最佳实践](#安全最佳实践)
8. [常见问题](#常见问题)

## 概述

本系统采用OAuth2授权码流程（Authorization Code Flow），为桌面游戏提供安全的用户授权登录服务。

### 流程概述

```
1. 游戏启动本地HTTP服务器
2. 游戏打开浏览器，跳转到授权页面
3. 用户在网页上登录（如未登录）
4. 用户确认授权
5. 服务器生成授权码，重定向回游戏
6. 游戏使用授权码交换访问令牌
7. 游戏使用访问令牌调用API
```

## 前置准备

### 1. 注册OAuth2客户端

在开始集成之前，您需要联系管理员注册OAuth2客户端，获取以下信息：

- `client_id`: 客户端ID
- `client_secret`: 客户端密钥（请妥善保管，不要泄露）
- `redirect_uri`: 回调URI（例如：`http://localhost:8080/callback`）

### 2. 配置redirect_uri

redirect_uri必须与注册时提供的URI完全匹配。支持以下格式：

- 精确匹配：`http://localhost:8080/callback`
- 通配符匹配：`http://localhost:*`（允许任意端口）

**重要**：redirect_uri必须使用HTTP协议（localhost）或HTTPS协议（生产环境）。

## OAuth2授权流程

### 步骤1：启动本地HTTP服务器

游戏需要启动一个本地HTTP服务器来接收授权回调。建议使用随机端口，避免端口冲突。

**示例（Unity C#）**：
```csharp
using System;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

public class OAuth2CallbackServer : MonoBehaviour
{
    private HttpListener listener;
    private string authorizationCode;
    private string state;
    private bool isListening = false;
    
    public void StartServer(int port, Action<string, string> onCallbackReceived)
    {
        listener = new HttpListener();
        listener.Prefixes.Add($"http://localhost:{port}/");
        listener.Start();
        isListening = true;
        
        Task.Run(async () =>
        {
            while (isListening)
            {
                try
                {
                    var context = await listener.GetContextAsync();
                    var request = context.Request;
                    var response = context.Response;
                    
                    var query = request.QueryString;
                    
                    if (query["code"] != null)
                    {
                        authorizationCode = query["code"];
                        state = query["state"];
                        
                        // 在主线程中调用回调
                        UnityMainThreadDispatcher.Instance.Enqueue(() =>
                        {
                            onCallbackReceived?.Invoke(authorizationCode, state);
                        });
                        
                        // 返回成功页面
                        string html = "<html><body><h1>Authorization successful!</h1><p>You can close this window.</p></body></html>";
                        byte[] buffer = Encoding.UTF8.GetBytes(html);
                        response.ContentLength64 = buffer.Length;
                        response.StatusCode = 200;
                        await response.OutputStream.WriteAsync(buffer, 0, buffer.Length);
                        response.Close();
                    }
                    else
                    {
                        // 错误处理
                        string error = query["error"] ?? "unknown_error";
                        string errorDescription = query["error_description"] ?? "";
                        string html = $"<html><body><h1>Authorization failed</h1><p>{error}: {errorDescription}</p></body></html>";
                        byte[] buffer = Encoding.UTF8.GetBytes(html);
                        response.ContentLength64 = buffer.Length;
                        response.StatusCode = 400;
                        await response.OutputStream.WriteAsync(buffer, 0, buffer.Length);
                        response.Close();
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogError($"Callback server error: {ex.Message}");
                }
            }
        });
    }
    
    public void StopServer()
    {
        isListening = false;
        listener?.Stop();
        listener?.Close();
    }
    
    // 获取可用端口
    public static int GetAvailablePort()
    {
        var listener = new HttpListener();
        listener.Prefixes.Add("http://localhost:0/");
        listener.Start();
        int port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        listener.Close();
        return port;
    }
}
```

### 步骤2：打开授权页面

构建授权URL并打开浏览器：

```
GET /oauth2/authorize?client_id={client_id}&redirect_uri={redirect_uri}&response_type=code&scope={scope}&state={state}
```

**参数说明**：

- `client_id`（必需）：客户端ID
- `redirect_uri`（必需）：回调URI，必须与注册的URI匹配
- `response_type`（必需）：固定为`code`
- `scope`（可选）：授权范围，空格分隔（例如：`read write`）
- `state`（可选）：状态参数，用于防止CSRF攻击

**示例（Unity C#）**：
```csharp
using UnityEngine;
using System;
using System.Security.Cryptography;
using System.Text;

public class OAuth2Authorization
{
    private string baseUrl = "https://your-domain.com";  // 替换为实际域名
    private string clientId = "your_client_id";
    
    public void OpenAuthorizationPage(int port, string scope = "read profile")
    {
        string redirectUri = $"http://localhost:{port}/callback";
        string state = GenerateRandomState();
        
        // 构建授权URL
        string authUrl = $"{baseUrl}/oauth2/authorize?" +
            $"client_id={Uri.EscapeDataString(clientId)}&" +
            $"redirect_uri={Uri.EscapeDataString(redirectUri)}&" +
            $"response_type=code&" +
            $"scope={Uri.EscapeDataString(scope)}&" +
            $"state={Uri.EscapeDataString(state)}";
        
        // 打开浏览器
        Debug.Log($"Opening browser: {authUrl}");
        Application.OpenURL(authUrl);
    }
    
    private string GenerateRandomState()
    {
        // 生成随机状态字符串
        byte[] randomBytes = new byte[16];
        using (RNGCryptoServiceProvider rng = new RNGCryptoServiceProvider())
        {
            rng.GetBytes(randomBytes);
        }
        return Convert.ToBase64String(randomBytes).Replace("+", "-").Replace("/", "_").Replace("=", "");
    }
}
```

### 步骤3：用户授权

用户在浏览器中：
1. 如果未登录，会先跳转到登录页面
2. 登录后，会看到授权确认页面
3. 用户点击"授权"按钮确认授权

### 步骤4：接收授权码

授权成功后，服务器会重定向到您的`redirect_uri`，并附带授权码：

```
http://localhost:8080/callback?code=AUTHORIZATION_CODE&state=STATE
```

从查询参数中提取`code`和`state`（如果提供了）。

**重要**：
- 授权码有效期：10分钟
- 授权码只能使用一次
- 验证`state`参数是否与步骤2中发送的一致

### 步骤5：交换访问令牌

使用授权码交换访问令牌：

```
POST /oauth2/token
Content-Type: application/json

{
  "grant_type": "authorization_code",
  "code": "AUTHORIZATION_CODE",
  "redirect_uri": "http://localhost:8080/callback",
  "client_id": "your_client_id",
  "client_secret": "your_client_secret"
}
```

**响应示例**：
后端返回统一响应格式：
```json
{
  "code": "200",
  "msg": "success",
  "data": {
    "access_token": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
    "token_type": "Bearer",
    "expires_in": 7200,
    "refresh_token": "refresh_token_string",
    "scope": "read profile"
  }
}
```

**注意**：需要从 `data` 字段中提取实际的令牌信息。

**响应字段说明**：

- `access_token`：访问令牌，用于调用API
- `token_type`：令牌类型，固定为`Bearer`
- `expires_in`：过期时间（秒），默认7200秒（2小时）
- `refresh_token`：刷新令牌，用于获取新的访问令牌
- `scope`：授权范围

### 步骤6：使用访问令牌调用API

在API请求的Header中添加访问令牌：

```
Authorization: Bearer {access_token}
```

**示例（Unity C#）**：
```csharp
using UnityEngine;
using UnityEngine.Networking;
using System.Collections;

IEnumerator GetUserInfo(string accessToken)
{
    string url = "https://your-domain.com/user/me";
    using (UnityWebRequest request = UnityWebRequest.Get(url))
    {
        request.SetRequestHeader("Authorization", $"Bearer {accessToken}");
        request.SetRequestHeader("Content-Type", "application/json");
        
        yield return request.SendWebRequest();
        
        if (request.result == UnityWebRequest.Result.Success)
        {
            string jsonResponse = request.downloadHandler.text;
            // 使用JsonUtility或Newtonsoft.Json解析响应
            // UserInfo userInfo = JsonUtility.FromJson<UserInfo>(jsonResponse);
        }
        else
        {
            Debug.LogError($"Error: {request.error}");
        }
    }
}
```

## API端点详细说明

### 重要：统一响应格式

**所有API响应都遵循统一的响应格式**：

```json
{
  "code": "200",    // 业务状态码，字符串类型。成功时为 "200" 或 "0"
  "msg": "success", // 响应消息
  "data": { ... }   // 实际数据，成功时包含响应内容，错误时为 null
}
```

**处理响应时的注意事项**：
1. 即使HTTP状态码是200，也需要检查响应中的 `code` 字段来判断业务是否成功
2. 成功时：`code` 为 `"200"` 或 `"0"`，实际数据在 `data` 字段中
3. 错误时：`code` 为错误码字符串（如 `"400"`, `"401"`），错误信息在 `msg` 字段中，`data` 为 `null`
4. 所有示例代码都需要先解析 `ApiResponse<T>` 格式，然后从 `data` 字段中提取实际数据

### GET /oauth2/authorize

授权端点，用于获取用户授权。

**请求参数**（查询参数）：

| 参数 | 类型 | 必需 | 说明 |
|------|------|------|------|
| client_id | string | 是 | 客户端ID |
| redirect_uri | string | 是 | 回调URI |
| response_type | string | 是 | 固定为`code` |
| scope | string | 否 | 授权范围，空格分隔 |
| state | string | 否 | 状态参数 |

**响应**：

- 如果用户未登录：返回401状态码，包含`loginUrl`字段
- 如果用户已登录：返回200状态码，包含客户端信息和授权范围

### POST /oauth2/token

令牌端点，用于交换授权码或刷新令牌。

**请求体**（JSON）：

**授权码模式**：
```json
{
  "grant_type": "authorization_code",
  "code": "AUTHORIZATION_CODE",
  "redirect_uri": "http://localhost:8080/callback",
  "client_id": "your_client_id",
  "client_secret": "your_client_secret"
}
```

**刷新令牌模式**：
```json
{
  "grant_type": "refresh_token",
  "refresh_token": "REFRESH_TOKEN",
  "client_id": "your_client_id",
  "client_secret": "your_client_secret"
}
```

**响应**：

成功（200）：
后端返回统一响应格式，需要从 `data` 字段中提取令牌信息：
```json
{
  "code": "200",
  "msg": "success",
  "data": {
    "access_token": "string",
    "token_type": "Bearer",
    "expires_in": 7200,
    "refresh_token": "string",
    "scope": "string"
  }
}
```

错误（400/401）：
```json
{
  "code": "400",
  "msg": "错误消息",
  "data": null
}
```

**重要**：成功响应时，需要检查响应中的 `code` 字段是否为 `"200"` 或 `"0"`，然后从 `data` 字段中提取实际的令牌信息。

## 集成示例

### Unity C#完整示例

以下是一个完整的Unity C#实现示例，包含所有必要的组件：

#### 1. OAuth2客户端主类

```csharp
using UnityEngine;
using UnityEngine.Networking;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Security.Cryptography;

[System.Serializable]
public class ApiResponse<T>
{
    public string code;
    public string msg;
    public T data;
}

[System.Serializable]
public class TokenResponse
{
    public string access_token;
    public string token_type;
    public int expires_in;
    public string refresh_token;
    public string scope;
}

[System.Serializable]
public class UserInfo
{
    public long id;
    public string username;
    public string email;
    // 根据实际API响应添加其他字段
}

public class OAuth2Client : MonoBehaviour
{
    [Header("OAuth2 Configuration")]
    [SerializeField] private string clientId = "your_client_id";
    [SerializeField] private string clientSecret = "your_client_secret";
    [SerializeField] private string baseUrl = "https://your-domain.com";
    [SerializeField] private string scope = "read profile";
    
    private string accessToken;
    private string refreshToken;
    private OAuth2CallbackServer callbackServer;
    private string currentState;
    private string currentRedirectUri;
    
    public event Action<string> OnAuthorizationCodeReceived;
    public event Action<TokenResponse> OnTokenReceived;
    public event Action<UserInfo> OnUserInfoReceived;
    public event Action<string> OnError;
    
    void Start()
    {
        callbackServer = gameObject.AddComponent<OAuth2CallbackServer>();
    }
    
    /// <summary>
    /// 开始授权流程
    /// </summary>
    public void StartAuthorization()
    {
        StartCoroutine(AuthorizeCoroutine());
    }
    
    private IEnumerator AuthorizeCoroutine()
    {
        // 1. 获取可用端口并启动回调服务器
        int port = OAuth2CallbackServer.GetAvailablePort();
        currentRedirectUri = $"http://localhost:{port}/callback";
        currentState = GenerateRandomState();
        
        callbackServer.StartServer(port, OnCallbackReceived);
        Debug.Log($"Callback server started on port {port}");
        
        // 2. 构建授权URL
        string authUrl = BuildAuthorizationUrl(currentRedirectUri, currentState);
        
        // 3. 打开浏览器
        Debug.Log($"Opening browser: {authUrl}");
        Application.OpenURL(authUrl);
        
        yield return null;
    }
    
    private void OnCallbackReceived(string code, string state)
    {
        // 验证state
        if (state != currentState)
        {
            OnError?.Invoke("State mismatch");
            return;
        }
        
        OnAuthorizationCodeReceived?.Invoke(code);
        
        // 停止回调服务器
        callbackServer.StopServer();
        
        // 交换令牌
        StartCoroutine(ExchangeTokenCoroutine(code, currentRedirectUri));
    }
    
    private string BuildAuthorizationUrl(string redirectUri, string state)
    {
        return $"{baseUrl}/oauth2/authorize?" +
            $"client_id={Uri.EscapeDataString(clientId)}&" +
            $"redirect_uri={Uri.EscapeDataString(redirectUri)}&" +
            $"response_type=code&" +
            $"scope={Uri.EscapeDataString(scope)}&" +
            $"state={Uri.EscapeDataString(state)}";
    }
    
    private string GenerateRandomState()
    {
        byte[] randomBytes = new byte[16];
        using (System.Security.Cryptography.RNGCryptoServiceProvider rng = 
               new System.Security.Cryptography.RNGCryptoServiceProvider())
        {
            rng.GetBytes(randomBytes);
        }
        return Convert.ToBase64String(randomBytes)
            .Replace("+", "-")
            .Replace("/", "_")
            .Replace("=", "");
    }
    
    /// <summary>
    /// 转义JSON字符串中的特殊字符
    /// </summary>
    private string EscapeJsonString(string value)
    {
        if (string.IsNullOrEmpty(value)) return "";
        return value.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "\\r").Replace("\t", "\\t");
    }
    
    /// <summary>
    /// 构建JSON请求体（更安全的方式）
    /// </summary>
    private string BuildJsonRequestBody(Dictionary<string, string> data)
    {
        var sb = new StringBuilder();
        sb.Append("{");
        bool first = true;
        foreach (var kvp in data)
        {
            if (!first) sb.Append(",");
            sb.Append($"\"{EscapeJsonString(kvp.Key)}\":\"{EscapeJsonString(kvp.Value)}\"");
            first = false;
        }
        sb.Append("}");
        return sb.ToString();
    }
    
    /// <summary>
    /// 使用授权码交换访问令牌
    /// </summary>
    private IEnumerator ExchangeTokenCoroutine(string authorizationCode, string redirectUri)
    {
        string url = $"{baseUrl}/oauth2/token";
        
        // 构建请求体
        var requestData = new Dictionary<string, string>
        {
            { "grant_type", "authorization_code" },
            { "code", authorizationCode },
            { "redirect_uri", redirectUri },
            { "client_id", clientId },
            { "client_secret", clientSecret }
        };
        string jsonBody = BuildJsonRequestBody(requestData);
        byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonBody);
        
        using (UnityWebRequest request = new UnityWebRequest(url, "POST"))
        {
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            
            yield return request.SendWebRequest();
            
            if (request.result == UnityWebRequest.Result.Success)
            {
                string jsonResponse = request.downloadHandler.text;
                
                // 解析统一响应格式
                ApiResponse<TokenResponse> apiResponse = JsonUtility.FromJson<ApiResponse<TokenResponse>>(jsonResponse);
                
                // 检查业务状态码
                if (apiResponse.code == "200" || apiResponse.code == "0")
                {
                    TokenResponse tokenData = apiResponse.data;
                    
                    accessToken = tokenData.access_token;
                    refreshToken = tokenData.refresh_token;
                    
                    OnTokenReceived?.Invoke(tokenData);
                    Debug.Log($"Access token received: {accessToken}");
                }
                else
                {
                    string error = $"Token exchange failed: {apiResponse.msg}";
                    Debug.LogError(error);
                    OnError?.Invoke(error);
                }
            }
            else
            {
                string error = $"Token exchange failed: {request.error}";
                Debug.LogError(error);
                OnError?.Invoke(error);
            }
        }
    }
    
    /// <summary>
    /// 刷新访问令牌
    /// </summary>
    public void RefreshAccessToken()
    {
        if (string.IsNullOrEmpty(refreshToken))
        {
            OnError?.Invoke("No refresh token available");
            return;
        }
        
        StartCoroutine(RefreshTokenCoroutine());
    }
    
    private IEnumerator RefreshTokenCoroutine()
    {
        string url = $"{baseUrl}/oauth2/token";
        
        // 构建请求体
        var requestData = new Dictionary<string, string>
        {
            { "grant_type", "refresh_token" },
            { "refresh_token", refreshToken },
            { "client_id", clientId },
            { "client_secret", clientSecret }
        };
        string jsonBody = BuildJsonRequestBody(requestData);
        byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonBody);
        
        using (UnityWebRequest request = new UnityWebRequest(url, "POST"))
        {
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            
            yield return request.SendWebRequest();
            
            if (request.result == UnityWebRequest.Result.Success)
            {
                string jsonResponse = request.downloadHandler.text;
                
                // 解析统一响应格式
                ApiResponse<TokenResponse> apiResponse = JsonUtility.FromJson<ApiResponse<TokenResponse>>(jsonResponse);
                
                // 检查业务状态码
                if (apiResponse.code == "200" || apiResponse.code == "0")
                {
                    TokenResponse tokenData = apiResponse.data;
                    
                    accessToken = tokenData.access_token;
                    refreshToken = tokenData.refresh_token;
                    
                    OnTokenReceived?.Invoke(tokenData);
                    Debug.Log($"Token refreshed: {accessToken}");
                }
                else
                {
                    string error = $"Token refresh failed: {apiResponse.msg}";
                    Debug.LogError(error);
                    OnError?.Invoke(error);
                }
            }
            else
            {
                string error = $"Token refresh failed: {request.error}";
                Debug.LogError(error);
                OnError?.Invoke(error);
            }
        }
    }
    
    /// <summary>
    /// 获取用户信息
    /// </summary>
    public void GetUserInfo()
    {
        if (string.IsNullOrEmpty(accessToken))
        {
            OnError?.Invoke("No access token available");
            return;
        }
        
        StartCoroutine(GetUserInfoCoroutine());
    }
    
    private IEnumerator GetUserInfoCoroutine()
    {
        string url = $"{baseUrl}/user/me";
        
        using (UnityWebRequest request = UnityWebRequest.Get(url))
        {
            request.SetRequestHeader("Authorization", $"Bearer {accessToken}");
            request.SetRequestHeader("Content-Type", "application/json");
            
            yield return request.SendWebRequest();
            
            if (request.result == UnityWebRequest.Result.Success)
            {
                string jsonResponse = request.downloadHandler.text;
                UserInfo userInfo = JsonUtility.FromJson<UserInfo>(jsonResponse);
                
                OnUserInfoReceived?.Invoke(userInfo);
                Debug.Log($"User info received: {userInfo.username}");
            }
            else
            {
                string error = $"Failed to get user info: {request.error}";
                Debug.LogError(error);
                OnError?.Invoke(error);
            }
        }
    }
    
    void OnDestroy()
    {
        callbackServer?.StopServer();
    }
}
```

#### 2. 回调服务器组件

```csharp
using System;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

public class OAuth2CallbackServer : MonoBehaviour
{
    private HttpListener listener;
    private bool isListening = false;
    private Action<string, string> onCallbackReceived;
    
    public void StartServer(int port, Action<string, string> callback)
    {
        onCallbackReceived = callback;
        listener = new HttpListener();
        listener.Prefixes.Add($"http://localhost:{port}/");
        listener.Start();
        isListening = true;
        
        Task.Run(async () =>
        {
            while (isListening && listener.IsListening)
            {
                try
                {
                    var context = await listener.GetContextAsync();
                    var request = context.Request;
                    var response = context.Response;
                    
                    var query = request.QueryString;
                    
                    if (query["code"] != null)
                    {
                        string code = query["code"];
                        string state = query["state"];
                        
                        // 在主线程中调用回调
                        UnityMainThreadDispatcher.Instance.Enqueue(() =>
                        {
                            onCallbackReceived?.Invoke(code, state);
                        });
                        
                        // 返回成功页面
                        string html = "<html><body><h1>Authorization successful!</h1><p>You can close this window.</p></body></html>";
                        byte[] buffer = Encoding.UTF8.GetBytes(html);
                        response.ContentLength64 = buffer.Length;
                        response.StatusCode = 200;
                        await response.OutputStream.WriteAsync(buffer, 0, buffer.Length);
                        response.Close();
                    }
                    else
                    {
                        // 错误处理
                        string error = query["error"] ?? "unknown_error";
                        string errorDescription = query["error_description"] ?? "";
                        string html = $"<html><body><h1>Authorization failed</h1><p>{error}: {errorDescription}</p></body></html>";
                        byte[] buffer = Encoding.UTF8.GetBytes(html);
                        response.ContentLength64 = buffer.Length;
                        response.StatusCode = 400;
                        await response.OutputStream.WriteAsync(buffer, 0, buffer.Length);
                        response.Close();
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogError($"Callback server error: {ex.Message}");
                }
            }
        });
    }
    
    public void StopServer()
    {
        isListening = false;
        listener?.Stop();
        listener?.Close();
    }
    
    /// <summary>
    /// 获取可用端口
    /// </summary>
    public static int GetAvailablePort()
    {
        var listener = new HttpListener();
        listener.Prefixes.Add("http://localhost:0/");
        listener.Start();
        int port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        listener.Close();
        return port;
    }
    
    void OnDestroy()
    {
        StopServer();
    }
}
```

#### 3. Unity主线程调度器（用于在后台线程中调用Unity API）

```csharp
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class UnityMainThreadDispatcher : MonoBehaviour
{
    private static UnityMainThreadDispatcher _instance;
    private static readonly Queue<Action> _executionQueue = new Queue<Action>();
    
    public static UnityMainThreadDispatcher Instance
    {
        get
        {
            if (_instance == null)
            {
                GameObject go = new GameObject("UnityMainThreadDispatcher");
                _instance = go.AddComponent<UnityMainThreadDispatcher>();
                DontDestroyOnLoad(go);
            }
            return _instance;
        }
    }
    
    void Update()
    {
        lock (_executionQueue)
        {
            while (_executionQueue.Count > 0)
            {
                _executionQueue.Dequeue().Invoke();
            }
        }
    }
    
    public void Enqueue(Action action)
    {
        lock (_executionQueue)
        {
            _executionQueue.Enqueue(action);
        }
    }
}
```

#### 4. 使用示例

```csharp
using UnityEngine;

public class OAuth2Example : MonoBehaviour
{
    private OAuth2Client oauth2Client;
    
    void Start()
    {
        // 创建OAuth2客户端
        GameObject oauth2Object = new GameObject("OAuth2Client");
        oauth2Client = oauth2Object.AddComponent<OAuth2Client>();
        
        // 订阅事件
        oauth2Client.OnAuthorizationCodeReceived += OnCodeReceived;
        oauth2Client.OnTokenReceived += OnTokenReceived;
        oauth2Client.OnUserInfoReceived += OnUserInfoReceived;
        oauth2Client.OnError += OnError;
        
        // 开始授权流程
        oauth2Client.StartAuthorization();
    }
    
    private void OnCodeReceived(string code)
    {
        Debug.Log($"Authorization code received: {code}");
    }
    
    private void OnTokenReceived(TokenResponse tokenData)
    {
        Debug.Log($"Access token: {tokenData.access_token}");
        Debug.Log($"Expires in: {tokenData.expires_in} seconds");
        
        // 获取用户信息
        oauth2Client.GetUserInfo();
    }
    
    private void OnUserInfoReceived(UserInfo userInfo)
    {
        Debug.Log($"User logged in: {userInfo.username} ({userInfo.email})");
        // 在这里处理用户登录成功后的逻辑
    }
    
    private void OnError(string error)
    {
        Debug.LogError($"OAuth2 Error: {error}");
    }
    
    void OnDestroy()
    {
        if (oauth2Client != null)
        {
            oauth2Client.OnAuthorizationCodeReceived -= OnCodeReceived;
            oauth2Client.OnTokenReceived -= OnTokenReceived;
            oauth2Client.OnUserInfoReceived -= OnUserInfoReceived;
            oauth2Client.OnError -= OnError;
        }
    }
}
```

#### 5. 令牌持久化示例

```csharp
using UnityEngine;

public class TokenStorage
{
    private const string ACCESS_TOKEN_KEY = "oauth2_access_token";
    private const string REFRESH_TOKEN_KEY = "oauth2_refresh_token";
    private const string TOKEN_EXPIRY_KEY = "oauth2_token_expiry";
    
    /// <summary>
    /// 保存令牌
    /// </summary>
    public static void SaveTokens(string accessToken, string refreshToken, int expiresIn)
    {
        PlayerPrefs.SetString(ACCESS_TOKEN_KEY, accessToken);
        PlayerPrefs.SetString(REFRESH_TOKEN_KEY, refreshToken);
        // 保存过期时间戳（当前时间 + 过期秒数）
        long expiryTimestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds() + expiresIn;
        PlayerPrefs.SetString(TOKEN_EXPIRY_KEY, expiryTimestamp.ToString());
        PlayerPrefs.Save();
    }
    
    /// <summary>
    /// 加载访问令牌
    /// </summary>
    public static string LoadAccessToken()
    {
        return PlayerPrefs.GetString(ACCESS_TOKEN_KEY, "");
    }
    
    /// <summary>
    /// 加载刷新令牌
    /// </summary>
    public static string LoadRefreshToken()
    {
        return PlayerPrefs.GetString(REFRESH_TOKEN_KEY, "");
    }
    
    /// <summary>
    /// 检查访问令牌是否过期
    /// </summary>
    public static bool IsAccessTokenExpired()
    {
        string expiryStr = PlayerPrefs.GetString(TOKEN_EXPIRY_KEY, "0");
        if (long.TryParse(expiryStr, out long expiryTimestamp))
        {
            long currentTimestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            return currentTimestamp >= expiryTimestamp;
        }
        return true; // 如果没有过期时间信息，认为已过期
    }
    
    /// <summary>
    /// 清除所有令牌
    /// </summary>
    public static void ClearTokens()
    {
        PlayerPrefs.DeleteKey(ACCESS_TOKEN_KEY);
        PlayerPrefs.DeleteKey(REFRESH_TOKEN_KEY);
        PlayerPrefs.DeleteKey(TOKEN_EXPIRY_KEY);
        PlayerPrefs.Save();
    }
}
```

#### 6. 注意事项

1. **Unity版本要求**：本示例适用于Unity 2019.4及以上版本
2. **JSON序列化**：
   - Unity内置的`JsonUtility`不支持嵌套对象和Dictionary，本示例提供了`BuildJsonRequestBody`方法来构建JSON
   - 如果使用Newtonsoft.Json（需要导入包），可以将`JsonUtility`替换为`JsonConvert`，并直接序列化Dictionary
3. **线程安全**：回调服务器在后台线程运行，需要使用`UnityMainThreadDispatcher`在主线程中调用Unity API
4. **令牌存储**：
   - 示例中提供了`TokenStorage`类用于持久化令牌
   - 生产环境建议使用更安全的存储方式（如加密存储）
   - 注意：`PlayerPrefs`在Windows上存储在注册表中，安全性较低
5. **错误处理**：在生产环境中，应该添加更完善的错误处理和重试机制
6. **平台兼容性**：
   - Windows、macOS、Linux都支持`HttpListener`
   - 移动平台（iOS/Android）可能需要使用不同的实现方式
7. **依赖项**：确保项目中已启用`.NET Standard 2.1`或`.NET Framework 4.x`以支持`HttpListener`

## 错误处理

### 错误响应格式

所有错误响应都遵循以下统一格式：

```json
{
  "code": "400",
  "msg": "错误消息",
  "data": null
}
```

**重要**：
- `code` 字段是字符串类型，成功时为 `"200"` 或 `"0"`，错误时为错误码字符串（如 `"400"`, `"401"`）
- 成功响应时，实际的令牌数据在 `data` 字段中
- 错误响应时，`data` 字段为 `null`，错误信息在 `msg` 字段中

### 常见错误码

| 错误码 | 说明 | 解决方案 |
|--------|------|----------|
| "400" | 请求参数错误 | 检查请求参数是否正确 |
| "401" | 未授权 | 检查客户端密钥是否正确，或重新授权 |
| "404" | 客户端不存在 | 检查client_id是否正确 |
| "400" | 授权码无效或已过期 | 重新执行授权流程 |
| "400" | redirect_uri不匹配 | 确保redirect_uri与注册的URI完全匹配 |

**注意**：错误码是字符串类型，需要与字符串 `"200"` 或 `"0"` 进行比较来判断成功。

### 错误处理示例（Unity C#）

```csharp
private IEnumerator ExchangeTokenWithErrorHandling(string authorizationCode, string redirectUri)
{
    string url = $"{baseUrl}/oauth2/token";
    
    // 构建请求体
    var requestData = new Dictionary<string, string>
    {
        { "grant_type", "authorization_code" },
        { "code", authorizationCode },
        { "redirect_uri", redirectUri },
        { "client_id", clientId },
        { "client_secret", clientSecret }
    };
    string jsonBody = BuildJsonRequestBody(requestData);
    byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonBody);
    
    using (UnityWebRequest request = new UnityWebRequest(url, "POST"))
    {
        request.uploadHandler = new UploadHandlerRaw(bodyRaw);
        request.downloadHandler = new DownloadHandlerBuffer();
        request.SetRequestHeader("Content-Type", "application/json");
        
        yield return request.SendWebRequest();
        
        if (request.result == UnityWebRequest.Result.Success)
        {
            string jsonResponse = request.downloadHandler.text;
            
            // 解析统一响应格式
            ApiResponse<TokenResponse> apiResponse = JsonUtility.FromJson<ApiResponse<TokenResponse>>(jsonResponse);
            
            // 检查业务状态码
            if (apiResponse.code == "200" || apiResponse.code == "0")
            {
                // 成功处理
                TokenResponse tokenData = apiResponse.data;
                Debug.Log($"Token received: {tokenData.access_token}");
                // 处理令牌数据
            }
            else
            {
                // 业务错误处理
                Debug.LogError($"Token exchange failed: {apiResponse.msg} (code: {apiResponse.code})");
            }
        }
        else
        {
            // 网络错误处理
            int statusCode = (int)request.responseCode;
            string errorMessage = request.downloadHandler.text;
            
            if (statusCode == 400)
            {
                // 尝试解析错误响应
                try
                {
                    var errorData = JsonUtility.FromJson<ErrorResponse>(errorMessage);
                    Debug.LogError($"OAuth2 Error: {errorData.msg}");
                    // 根据错误类型采取相应措施
                }
                catch
                {
                    Debug.LogError($"OAuth2 Error: {errorMessage}");
                }
            }
            else
            {
                Debug.LogError($"Unexpected error: {request.error} (Status: {statusCode})");
            }
        }
    }
}

[System.Serializable]
public class ErrorResponse
{
    public string code;  // 注意：code是字符串类型，如 "400", "401" 等
    public string msg;
    public object data;
}
```

## 安全最佳实践

### 1. 保护客户端密钥

- **永远不要**将`client_secret`硬编码在客户端代码中
- 使用配置文件或环境变量存储密钥
- 不要将密钥提交到版本控制系统

### 2. 使用HTTPS（生产环境）

- 在生产环境中，必须使用HTTPS
- 本地开发可以使用HTTP（localhost）

### 3. 验证state参数

- 始终使用随机生成的`state`参数
- 验证回调中的`state`是否与发送的一致
- 防止CSRF攻击

### 4. 安全存储令牌（Unity特定）

- **不要使用PlayerPrefs存储敏感信息**：PlayerPrefs在Windows上存储在注册表中，容易被读取
- **推荐方案**：
  - Windows：使用Windows Credential Manager（需要P/Invoke）
  - macOS：使用Keychain（需要原生插件）
  - Linux：使用加密的配置文件
  - 或者使用Unity的加密存储方案（如AES加密后存储）
- **示例**：可以使用`System.Security.Cryptography`对令牌进行加密后再存储
- 不要将令牌存储在Unity的Resources文件夹中（会被打包到游戏中）

### 5. 及时刷新令牌

- 在访问令牌过期前使用刷新令牌获取新令牌
- 不要频繁刷新令牌，避免不必要的请求

### 6. 处理令牌过期

- 实现令牌过期检测
- 自动使用刷新令牌获取新令牌
- 如果刷新令牌也过期，重新执行授权流程

## 常见问题

### Q1: 授权码过期了怎么办？

A: 授权码有效期为10分钟。如果过期，需要重新执行授权流程。

### Q2: 访问令牌过期了怎么办？

A: 使用刷新令牌调用`/oauth2/token`端点（grant_type=refresh_token）获取新的访问令牌。

### Q3: 刷新令牌也过期了怎么办？

A: 刷新令牌有效期为30天。如果过期，需要用户重新授权。

### Q4: 如何撤销授权？

A: 目前不支持撤销授权。用户可以联系管理员撤销客户端授权。

### Q5: 支持哪些scope？

A: 当前支持的scope包括：
- `read`: 读取权限
- `write`: 写入权限
- `profile`: 访问个人资料

### Q6: redirect_uri必须完全匹配吗？

A: 是的，redirect_uri必须与注册时提供的URI完全匹配。支持通配符匹配（如`http://localhost:*`）。

### Q7: 可以在多个端口上使用同一个redirect_uri吗？

A: 可以，如果注册时使用了通配符（如`http://localhost:*`），可以在任意端口使用。

### Q8: Unity中HttpListener在移动平台上可用吗？

A: HttpListener在iOS和Android上不可用。对于移动平台，建议：
- 使用自定义URL Scheme（如`mygame://callback`）
- 或者使用服务器端代理方案
- 或者使用OAuth2的设备码流程（Device Code Flow）

### Q9: Unity中如何安全地存储client_secret？

A: 建议：
- 使用Unity的ScriptableObject存储配置（不提交到版本控制）
- 使用环境变量或配置文件（.gitignore）
- 考虑使用服务器端代理，避免在客户端存储client_secret
- 对于公开客户端，可以考虑不验证client_secret（需要服务器支持）

## 技术支持

如有问题，请联系技术支持团队。

---

**文档版本**: 1.0  
**最后更新**: 2026-02-09  
**适用平台**: Unity 2019.4+ (Windows/macOS/Linux)
