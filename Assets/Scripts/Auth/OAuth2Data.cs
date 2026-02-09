using Newtonsoft.Json;

/// <summary>
/// OAuth2 令牌响应（data 内容）
/// </summary>
[System.Serializable]
public class OAuth2TokenResponse
{
    [JsonProperty("access_token")]
    public string AccessToken { get; set; }

    [JsonProperty("token_type")]
    public string TokenType { get; set; }

    [JsonProperty("expires_in")]
    public int ExpiresIn { get; set; }

    [JsonProperty("refresh_token")]
    public string RefreshToken { get; set; }

    [JsonProperty("scope")]
    public string Scope { get; set; }
}

/// <summary>
/// 用户信息（/user/me 返回的 data 内容）
/// </summary>
[System.Serializable]
public class OAuth2UserInfo
{
    [JsonProperty("id")]
    public long Id { get; set; }

    [JsonProperty("username")]
    public string Username { get; set; }

    [JsonProperty("name")]
    public string Name { get; set; }

    [JsonProperty("email")]
    public string Email { get; set; }

    [JsonProperty("nickname")]
    public string Nickname { get; set; }

    [JsonProperty("avatar")]
    public string Avatar { get; set; }

    [JsonProperty("avatarUrl")]
    public string AvatarUrl { get; set; }

    [JsonProperty("avatar_url")]
    public string Avatar_Url { get; set; }
}
