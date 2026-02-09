using System;
using System.Collections;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// OAuth2 客户端，处理授权流程
/// </summary>
public class OAuth2Client : MonoBehaviour
{
    private const string UiBaseUrl = "https://aqr.azusa.site";
    private const string ApiBaseUrl = "https://api.azusa.site";
    private const string ClientId = "aqr_mod";
    private const string ClientSecret = "bvKXC}Y=UXJJ!h.03zP#Arj4+bcTTFL64C8V52>vx0:py-0G}%6e2,6>Z!wU_CND";
    private const string Scope = "read profile";

    private OAuth2CallbackServer callbackServer;
    private string currentState;
    private string currentRedirectUri;

    public event Action<OAuth2UserInfo> OnLoginSuccess;
    public event Action<string> OnLoginFail;

    void Awake()
    {
        callbackServer = gameObject.AddComponent<OAuth2CallbackServer>();
        UnityMainThreadDispatcher.Instance.Enqueue(null);
    }

    public void StartLogin()
    {
        StartCoroutine(LoginCoroutine());
    }

    private IEnumerator LoginCoroutine()
    {
        int port = OAuth2CallbackServer.CallbackPort;
        currentRedirectUri = $"http://localhost:{port}/callback";
        currentState = GenerateRandomState();

        callbackServer.StartServer(port, OnCallbackReceived);

        string authUrl = $"{UiBaseUrl}/oauth2/authorize?" +
            $"client_id={Uri.EscapeDataString(ClientId)}&" +
            $"redirect_uri={Uri.EscapeDataString(currentRedirectUri)}&" +
            $"response_type=code&" +
            $"scope={Uri.EscapeDataString(Scope)}&" +
            $"state={Uri.EscapeDataString(currentState)}";

        Application.OpenURL(authUrl);
        yield return null;
    }

    private void OnCallbackReceived(string code, string state)
    {
        if (state != currentState)
        {
            OnLoginFail?.Invoke("State 验证失败，请重试");
            callbackServer.StopServer();
            return;
        }

        callbackServer.StopServer();
        StartCoroutine(ExchangeTokenAndGetUserInfo(code));
    }

    private IEnumerator ExchangeTokenAndGetUserInfo(string authorizationCode)
    {
        var tokenRequest = new
        {
            grant_type = "authorization_code",
            code = authorizationCode,
            redirect_uri = currentRedirectUri,
            client_id = ClientId,
            client_secret = ClientSecret
        };

        string jsonBody = JsonConvert.SerializeObject(tokenRequest);
        byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonBody);

        using (var request = new UnityWebRequest($"{ApiBaseUrl}/oauth2/token", "POST"))
        {
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");

            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                OnLoginFail?.Invoke($" Token 请求失败: {request.error}");
                yield break;
            }

            var wrapper = JsonConvert.DeserializeObject<ApiResponseWrapper<OAuth2TokenResponse>>(request.downloadHandler.text);
            if (wrapper == null || wrapper.Data == null)
            {
                OnLoginFail?.Invoke("Token 响应解析失败");
                yield break;
            }

            if (wrapper.Code != "200" && wrapper.Code != "0")
            {
                OnLoginFail?.Invoke(wrapper.Msg ?? "Token 交换失败");
                yield break;
            }

            var tokenData = wrapper.Data;
            TokenStorage.SaveTokens(tokenData.AccessToken, tokenData.RefreshToken, tokenData.ExpiresIn);

            yield return GetUserInfoCoroutine(tokenData.AccessToken);
        }
    }

    private IEnumerator GetUserInfoCoroutine(string accessToken)
    {
        string url = $"{ApiBaseUrl}/user/me";
        Debug.Log($"[OAuth2] /user/me 请求:\n  URL: {url}\n  Method: GET\n  Headers: Authorization: Bearer {accessToken.Substring(0, Math.Min(20, accessToken.Length))}...");

        using (var request = UnityWebRequest.Get(url))
        {
            request.SetRequestHeader("Authorization", $"Bearer {accessToken}");
            request.SetRequestHeader("Content-Type", "application/json");

            yield return request.SendWebRequest();

            string responseText = request.downloadHandler.text;
            Debug.Log($"[OAuth2] /user/me 响应:\n  Status: {request.responseCode}\n  Body:\n{responseText}");

            if (request.result != UnityWebRequest.Result.Success)
            {
                OnLoginSuccess?.Invoke(new OAuth2UserInfo { Username = "已登录" });
                yield break;
            }

            var wrapper = JsonConvert.DeserializeObject<ApiResponseWrapper<OAuth2UserInfo>>(responseText);
            if (wrapper != null && wrapper.Data != null && (wrapper.Code == "200" || wrapper.Code == "0"))
            {
                var data = wrapper.Data;
                string username = data.Username ?? data.Nickname ?? data.Name ?? "";
                TokenStorage.UpdateUsername(username);
                string avatar = data.Avatar ?? data.AvatarUrl ?? data.Avatar_Url ?? "";
                if (!string.IsNullOrEmpty(avatar))
                    TokenStorage.UpdateAvatarUrl(avatar);
                OnLoginSuccess?.Invoke(data);
            }
            else
            {
                OnLoginSuccess?.Invoke(new OAuth2UserInfo { Username = "已登录" });
            }
        }
    }

    private string GenerateRandomState()
    {
        byte[] randomBytes = new byte[16];
        using (var rng = RandomNumberGenerator.Create())
        {
            rng.GetBytes(randomBytes);
        }
        return Convert.ToBase64String(randomBytes).Replace("+", "-").Replace("/", "_").Replace("=", "");
    }

    void OnDestroy()
    {
        callbackServer?.StopServer();
    }

    [Serializable]
    private class ApiResponseWrapper<T>
    {
        [JsonProperty("code")]
        public string Code;

        [JsonProperty("msg")]
        public string Msg;

        [JsonProperty("data")]
        public T Data;
    }
}
