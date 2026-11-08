using UnityEngine;

// DeviceVisibility is a component that shows or hides the mobile and desktop controls based on the platform.

public class DeviceVisibility : MonoBehaviour
{
    [SerializeField] private GameObject mobile;
    [SerializeField] private GameObject desktop;

    private void Start()
    {
        bool isMobile = Application.isMobilePlatform;

#if UNITY_EDITOR
        isMobile = false;
#endif

        mobile.SetActive(isMobile);
        desktop.SetActive(!isMobile);
    }
}
