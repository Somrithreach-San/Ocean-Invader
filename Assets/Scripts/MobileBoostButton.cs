using UnityEngine;
using UnityEngine.EventSystems;

public class MobileBoostButton : MonoBehaviour, IPointerDownHandler
{
    public static MobileBoostButton Instance;
    
    private int lastPressedFrame = -1;
    public bool WasPressedThisFrame => lastPressedFrame == Time.frameCount;
    
    private void Awake()
    {
        Instance = this;
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        lastPressedFrame = Time.frameCount;
    }
}
