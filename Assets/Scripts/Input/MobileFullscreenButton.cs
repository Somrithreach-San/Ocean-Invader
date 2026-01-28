using UnityEngine;
using UnityEngine.UI;

public class MobileFullscreenButton : MonoBehaviour
{
    private Button btn;

    void Start()
    {
        btn = GetComponent<Button>();
        if (btn == null) btn = gameObject.AddComponent<Button>();
        
        btn.onClick.AddListener(ToggleFullscreen);
    }

    public void ToggleFullscreen()
    {
        // Toggle Fullscreen
        // Note: On WebGL Mobile, this requires a user gesture (which the button click provides).
        Screen.fullScreen = !Screen.fullScreen;
        Debug.Log("Toggled Fullscreen: " + Screen.fullScreen);
    }
}
