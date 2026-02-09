using UnityEngine;
using UnityEngine.SceneManagement;

public class SwitchTheScene : MonoBehaviour
{
    public GameObject fadeObject;
    public CanvasGroup panel;
    
    private static SwitchTheScene instance;

    void Awake()
    {
        instance = this;
        if (panel != null)
        {
            panel.alpha = 0;
        }
    }

    public static void SwitchScene(string sceneName)
    {
        if (instance != null && instance.panel != null)
        {
            instance.StartCoroutine(instance.FadeInAndSwitchScene(sceneName));
        }
        else
        {
            SceneManager.LoadScene(sceneName);
        }
    }

    private System.Collections.IEnumerator FadeInAndSwitchScene(string sceneName)
    {
        if (fadeObject != null)
        {
            fadeObject.SetActive(true);
        }
        
        float duration = 1f;
        float elapsedTime = 0f;
        
        while (elapsedTime < duration)
        {
            panel.alpha = Mathf.Lerp(0, 1, elapsedTime / duration);
            elapsedTime += Time.deltaTime;
            yield return null;
        }
        panel.alpha = 1;
        SceneManager.LoadScene(sceneName);
    }
}
