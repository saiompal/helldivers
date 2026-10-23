using UnityEngine;
using UnityEngine.InputSystem;

public class MobileControlsVisibility : MonoBehaviour
{
    [SerializeField] private GameObject mobileControls;

    private void Awake()
    {
        bool showMobileControls = 
            Application.isMobilePlatform ||
            (Application.platform == RuntimePlatform.WebGLPlayer &&
             Touchscreen.current != null);

        if (mobileControls != null)
        {
            mobileControls.SetActive(showMobileControls);
        }
    }
}
