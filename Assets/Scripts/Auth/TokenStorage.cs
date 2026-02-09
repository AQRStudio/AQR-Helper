using System;
using UnityEngine;

/// <summary>
/// OAuth2 令牌持久化存储
/// </summary>
public static class TokenStorage
{
    private const string AccessTokenKey = "oauth2_access_token";
    private const string RefreshTokenKey = "oauth2_refresh_token";
    private const string TokenExpiryKey = "oauth2_token_expiry";
    private const string UsernameKey = "oauth2_username";
    private const string AvatarUrlKey = "oauth2_avatar_url";

    public static void SaveTokens(string accessToken, string refreshToken, int expiresIn, string username = "", string avatarUrl = "")
    {
        PlayerPrefs.SetString(AccessTokenKey, accessToken);
        PlayerPrefs.SetString(RefreshTokenKey, refreshToken);
        long expiryTimestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds() + expiresIn;
        PlayerPrefs.SetString(TokenExpiryKey, expiryTimestamp.ToString());
        if (!string.IsNullOrEmpty(username))
            PlayerPrefs.SetString(UsernameKey, username);
        if (!string.IsNullOrEmpty(avatarUrl))
            PlayerPrefs.SetString(AvatarUrlKey, avatarUrl);
        PlayerPrefs.Save();
    }

    public static void UpdateUsername(string username)
    {
        if (!string.IsNullOrEmpty(username))
        {
            PlayerPrefs.SetString(UsernameKey, username);
            PlayerPrefs.Save();
        }
    }

    public static string LoadAvatarUrl()
    {
        return PlayerPrefs.GetString(AvatarUrlKey, "");
    }

    public static void UpdateAvatarUrl(string avatarUrl)
    {
        if (!string.IsNullOrEmpty(avatarUrl))
        {
            PlayerPrefs.SetString(AvatarUrlKey, avatarUrl);
            PlayerPrefs.Save();
        }
    }

    public static string LoadAccessToken()
    {
        return PlayerPrefs.GetString(AccessTokenKey, "");
    }

    public static string LoadRefreshToken()
    {
        return PlayerPrefs.GetString(RefreshTokenKey, "");
    }

    public static string LoadUsername()
    {
        return PlayerPrefs.GetString(UsernameKey, "");
    }

    public static bool IsAccessTokenExpired()
    {
        string expiryStr = PlayerPrefs.GetString(TokenExpiryKey, "0");
        if (long.TryParse(expiryStr, out long expiryTimestamp))
        {
            long current = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            return current >= expiryTimestamp;
        }
        return true;
    }

    public static bool HasValidToken()
    {
        return !string.IsNullOrEmpty(LoadAccessToken()) && !IsAccessTokenExpired();
    }

    public static void ClearTokens()
    {
        PlayerPrefs.DeleteKey(AccessTokenKey);
        PlayerPrefs.DeleteKey(RefreshTokenKey);
        PlayerPrefs.DeleteKey(TokenExpiryKey);
        PlayerPrefs.DeleteKey(UsernameKey);
        PlayerPrefs.DeleteKey(AvatarUrlKey);
        PlayerPrefs.Save();
    }
}
