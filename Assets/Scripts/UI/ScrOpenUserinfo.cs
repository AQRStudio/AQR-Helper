using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Networking;
using DG.Tweening;
using TMPro;

public class ScrOpenUserinfo : MonoBehaviour
{
    public Button btnOpenUserinfo;
    public GameObject userinfoPanel;
    public Button LoginButton;
    public TMP_Text UserNameText;
    [Header("头像")]
    public RawImage avatarImage1;
    public RawImage avatarImage2;
    [Tooltip("未登录时显示的默认头像")]
    public Texture2D defaultAvatar;
    public static bool isLogin = false;
    public bool isOpen = false;

    private OAuth2Client oauth2Client;
    private Coroutine loadAvatarCoroutine;
    private int logoutClickCount;
    private float logoutClickTime;

    void Start()
    {
        btnOpenUserinfo.onClick.AddListener(OnClickOpenUserinfo);
        if (LoginButton != null)
            LoginButton.onClick.AddListener(OnLoginClick);

        oauth2Client = gameObject.AddComponent<OAuth2Client>();
        oauth2Client.OnLoginSuccess += OnLoginSuccess;
        oauth2Client.OnLoginFail += OnLoginFail;

        CheckSavedLogin();
        UpdateUserNameText();
        UpdateAvatarDisplay();
    }

    void OnDestroy()
    {
        if (oauth2Client != null)
        {
            oauth2Client.OnLoginSuccess -= OnLoginSuccess;
            oauth2Client.OnLoginFail -= OnLoginFail;
        }
    }

    /// <summary>
    /// 检查本地是否已有有效登录
    /// </summary>
    void CheckSavedLogin()
    {
        if (TokenStorage.HasValidToken())
        {
            isLogin = true;
            string savedUsername = TokenStorage.LoadUsername();
            if (!string.IsNullOrEmpty(savedUsername) && UserNameText != null)
                UserNameText.text = savedUsername;
        }
    }

    void OnLoginClick()
    {
        if (isLogin)
        {
            float now = Time.unscaledTime;
            if (now - logoutClickTime > 1f) logoutClickCount = 0;
            logoutClickTime = now;
            logoutClickCount++;
            if (logoutClickCount >= 3)
            {
                DoLogout();
                logoutClickCount = 0;
            }
            else
            {
                ScrAlert.Show("您已登录");
            }
            return;
        }

        if (LoginButton != null)
            LoginButton.interactable = false;

        ScrAlert.Show("正在打开浏览器，请完成授权...", false);
        oauth2Client.StartLogin();
    }

    void DoLogout()
    {
        TokenStorage.ClearTokens();
        isLogin = false;
        UpdateUserNameText();
        UpdateAvatarDisplay();
        ScrAlert.Show("已退出登录");
    }

    void OnLoginSuccess(OAuth2UserInfo userInfo)
    {
        isLogin = true;
        if (LoginButton != null)
            LoginButton.interactable = true;

        ScrAlert.Hide();
        ScrAlert.Show("登录成功");

        string displayName = userInfo.Nickname ?? userInfo.Username ?? userInfo.Name ?? "已登录";
        UpdateUserNameText(displayName);
        string avatar = userInfo.Avatar ?? userInfo.AvatarUrl ?? userInfo.Avatar_Url ?? "";
        LoadUserAvatar(avatar);
    }

    void OnLoginFail(string error)
    {
        if (LoginButton != null)
            LoginButton.interactable = true;

        ScrAlert.Hide();
        ScrAlert.Show(error ?? "登录失败");
    }

    void OnClickOpenUserinfo()
    {
        isOpen = !isOpen;
        if (isOpen)
        {
            userinfoPanel.transform.DOScale(1, 0.3f).SetEase(Ease.OutQuad);
        }
        else
        {
            userinfoPanel.transform.DOScale(0, 0.3f).SetEase(Ease.OutQuad);
        }
    }

    void UpdateUserNameText(string displayName = null)
    {
        if (UserNameText == null) return;

        if (isLogin)
        {
            UserNameText.text = !string.IsNullOrEmpty(displayName)
                ? displayName
                : (TokenStorage.LoadUsername() ?? "已登录");
        }
        else
        {
            UserNameText.text = "请先登录";
        }
    }

    /// <summary>
    /// 根据登录状态更新头像显示
    /// </summary>
    void UpdateAvatarDisplay()
    {
        if (isLogin)
        {
            string avatarUrl = TokenStorage.LoadAvatarUrl();
            if (!string.IsNullOrEmpty(avatarUrl))
                LoadUserAvatar(avatarUrl);
            else
                SetAvatarTexture(defaultAvatar);
        }
        else
        {
            SetAvatarTexture(defaultAvatar);
        }
    }

    void LoadUserAvatar(string url)
    {
        if (string.IsNullOrEmpty(url))
        {
            SetAvatarTexture(defaultAvatar);
            return;
        }

        if (loadAvatarCoroutine != null)
            StopCoroutine(loadAvatarCoroutine);

        loadAvatarCoroutine = StartCoroutine(LoadAvatarCoroutine(url));
    }

    IEnumerator LoadAvatarCoroutine(string url)
    {
        using (var request = UnityWebRequestTexture.GetTexture(url))
        {
            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                var texture = DownloadHandlerTexture.GetContent(request);
                SetAvatarTexture(texture);
            }
            else
            {
                SetAvatarTexture(defaultAvatar);
            }
        }
        loadAvatarCoroutine = null;
    }

    void SetAvatarTexture(Texture texture)
    {
        if (avatarImage1 != null)
            avatarImage1.texture = texture ?? defaultAvatar;
        if (avatarImage2 != null)
            avatarImage2.texture = texture ?? defaultAvatar;
    }
}
