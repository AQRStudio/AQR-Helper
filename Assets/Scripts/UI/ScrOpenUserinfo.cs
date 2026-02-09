using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;
using TMPro;

public class ScrOpenUserinfo : MonoBehaviour
{
    public Button btnOpenUserinfo;
    public GameObject userinfoPanel;
    public Button LoginButton;
    public TMP_Text UserNameText;
    public static bool isLogin = false;
    public bool isOpen = false;

    void Start()
    {
        btnOpenUserinfo.onClick.AddListener(OnClickOpenUserinfo);
        UpdateUserNameText();
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

    void UpdateUserNameText()
    {
        if (isLogin)
        {
            // TODO: Set the username when available
            UserNameText.text = "";
        }
        else
        {
            UserNameText.text = "请先登录";
        }
    }
}
