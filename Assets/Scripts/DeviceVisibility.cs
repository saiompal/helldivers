using UnityEngine;
using System.Runtime.InteropServices;

// DeviceVisibility is a component that shows or hides the mobile and desktop controls based on the platform.

public class DeviceVisibility : MonoBehaviour
{
    [SerializeField] private GameObject mobile;
    [SerializeField] private GameObject desktop;

#if UNITY_WEBGL && !UNITY_EDITOR
    [DllImport("__Internal")]
    private static extern int IsMobileBrowser();
#endif

    public static bool IsMobileDevice
    {
        get
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            return IsMobileBrowser() != 0;
#elif UNITY_EDITOR
            return false;
#else
            return Application.isMobilePlatform;
#endif
        }
    }

    private void Start()
    {
        bool isMobile = IsMobileDevice;
        if (mobile != null) mobile.SetActive(isMobile);
        if (desktop != null) desktop.SetActive(!isMobile);
    }
}
