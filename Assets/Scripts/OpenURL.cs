using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class OpenURL : MonoBehaviour
{
    public string url;
    // Start is called before the first frame update
    void Start()
    {
        GetComponent<Button>().onClick.AddListener(OpenLink);
    }

    // Update is called once per frame
    private void OpenLink()
    {
        Application.OpenURL(url);
    }
}
