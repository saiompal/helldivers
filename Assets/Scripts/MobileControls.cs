using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;
using System.Collections.Generic;
using System.Runtime.InteropServices;

// MobileControls is a component that reads tilt and touch input on mobile devices and provides it to PlayerMovement. It is not used on desktop platforms.

public class MobileControls : MonoBehaviour
{
    public float Horizontal { get; private set; }

    private PlayerMovement playerMovement;

#if UNITY_WEBGL && !UNITY_EDITOR
    [DllImport("__Internal")]
    private static extern void StartTiltSensor();

    [DllImport("__Internal")]
    private static extern float GetTiltValue();
#endif

    private void Awake()
    {
        playerMovement = GetComponent<PlayerMovement>();
    }

    private void Start()
    {
        if (!DeviceVisibility.IsMobileDevice)
        {
            enabled = false;
            return;
        }
#if UNITY_WEBGL && !UNITY_EDITOR
        StartTiltSensor();
#endif
    }

    private void Update()
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        // Tilt to move left/right
        Horizontal = Mathf.Clamp(GetTiltValue(), -1f, 1f);

        // Tap the game screen to jump
        if (Touchscreen.current != null &&
            Touchscreen.current.primaryTouch.press.wasPressedThisFrame)
        {
            Vector2 position =
                Touchscreen.current.primaryTouch.position.ReadValue();

            if (!IsTouchOverUI(position))
            {
                playerMovement?.RequestJump();
            }
        }
#endif
    }

    private bool IsTouchOverUI(Vector2 position)
    {
        if (EventSystem.current == null)
            return false;

        var eventData = new PointerEventData(EventSystem.current)
        {
            position = position
        };

        var results = new List<RaycastResult>();
        EventSystem.current.RaycastAll(eventData, results);

        return results.Count > 0;
    }
}
