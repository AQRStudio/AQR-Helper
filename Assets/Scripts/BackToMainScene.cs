using UnityEngine;

public class SwitchTheSecne : MonoBehaviour
{
    public CanvasGroup Panel;
    // Start is called before the first frame update
    void Start()
    {
        Panel.alpha = 0;
    }

    // Update is called once per frame
    public void SwitchSecne()
    {
        StartCoroutine(FadeIn());
    }

    private System.Collections.IEnumerator FadeIn()
    {
        float duration = 1f;
        float elapsedTime = 0f;
        
        while (elapsedTime < duration)
        {
            Panel.alpha = Mathf.Lerp(0, 1, elapsedTime / duration);
            elapsedTime += Time.deltaTime;
            yield return null;
        }
        
        Panel.alpha = 1;
    }
}
