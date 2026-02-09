using UnityEngine;
using UnityEngine.UI;

public class GoToMainScene : MonoBehaviour
{
    void Start()
    {
        
        GetComponent<Button>().onClick.AddListener(GoMainScene);
    }

    public void GoMainScene()
    {
        SwitchTheScene.SwitchScene("scnMenu");
    }
}
