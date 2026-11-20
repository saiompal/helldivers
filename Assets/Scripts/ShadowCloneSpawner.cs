using UnityEngine;
using UnityEngine.InputSystem;
using TMPro;
using UnityEngine.UI;

public class ShadowCloneSpawner : MonoBehaviour
{
    [SerializeField] private GameObject shadowPrefab;

    private GameObject currentShadow;
    public GameObject CurrentShadow => currentShadow;
    private Canvas canvas;
    private TMP_Text hint;
    private Button mobileButton;

    private void Start()
    {
        canvas = FindAnyObjectByType<Canvas>()?.rootCanvas;
        if (canvas == null) return;
        hint = GameUi.Text("Shadow Hint", canvas.transform,
            DeviceVisibility.IsMobileDevice
                ? "Tap CLONE to leave a shadow on a pressure button and open its matching walls."
                : "<b>Q</b> Leave a shadow on a pressure button to hold its matching walls open.",
            24, Vector2.zero, new Vector2(1100, 70));
        hint.enableAutoSizing = true;
        hint.fontSizeMin = 14;
        hint.rectTransform.anchorMin = hint.rectTransform.anchorMax = new Vector2(.5f, 0);
        hint.rectTransform.anchoredPosition = new Vector2(0, 60);
        if (!DeviceVisibility.IsMobileDevice) return;
        mobileButton = GameUi.Button("Mobile Clone", canvas.transform,
            "CLONE", Vector2.zero, new Vector2(160, 160), false, false);
        mobileButton.onClick.AddListener(RequestShadow);
        var navigation = mobileButton.navigation;
        navigation.mode = Navigation.Mode.None;
        mobileButton.navigation = navigation;
    }

    private void LateUpdate()
    {
        bool finished = GameManager.Instance != null && GameManager.Instance.IsFinished;
        if (hint != null)
        {
            hint.gameObject.SetActive(!finished && (GameManager.Instance == null || !GameManager.Instance.IsTutorial));
            float scale = Mathf.Max(canvas.scaleFactor, .001f);
            hint.rectTransform.sizeDelta = new Vector2(
                Mathf.Min(1100, (Screen.safeArea.width - 32f) / scale), 70);
            hint.rectTransform.anchoredPosition = new Vector2(0,
                Screen.safeArea.yMin / scale + 60);
        }
        if (mobileButton == null) return;
        float uiScale = Mathf.Max(canvas.scaleFactor, .001f);
        float size = Mathf.Clamp(Mathf.Min(Screen.width, Screen.height) * .16f, 64, 88) / uiScale;
        var rect = (RectTransform)mobileButton.transform;
        rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.zero;
        rect.sizeDelta = Vector2.one * size;
        rect.anchoredPosition = new Vector2(
            (Screen.safeArea.xMin + 20) / uiScale, (Screen.safeArea.yMin + 20) / uiScale);
        var label = mobileButton.transform.Find("Label").GetComponent<TMP_Text>();
        label.fontSize = size * .18f;
        label.rectTransform.sizeDelta = Vector2.one * size;
        hint.gameObject.SetActive(!finished && GameManager.Instance != null &&
            GameManager.Instance.IsPreviewing && !GameManager.Instance.IsTutorial);
        hint.rectTransform.anchoredPosition = new Vector2(0,
            (Screen.safeArea.yMin + 20) / uiScale + size + 50);
        mobileButton.gameObject.SetActive(!finished);
        mobileButton.interactable = GameManager.Instance == null || GameManager.Instance.CanPlayerMove;
    }

    private void Update()
    {
        if (Keyboard.current == null)
            return;

        // 预览、地图翻转、胜利或失败期间不生成分身。
        if (GameManager.Instance != null &&
            !GameManager.Instance.CanPlayerMove)
        {
            return;
        }

        if (Keyboard.current.qKey.wasPressedThisFrame)
        {
            RequestShadow();
        }
    }

    public void RequestShadow()
    {
        if (!isActiveAndEnabled ||
            (GameManager.Instance != null && !GameManager.Instance.CanPlayerMove)) return;
        CreateShadow();
    }

    private void CreateShadow()
    {
        if (shadowPrefab == null)
        {
            Debug.LogWarning("请先在 Inspector 中设置 Shadow Prefab。", this);
            return;
        }

        // 同时只保留一个分身。
        if (currentShadow != null)
        {
            currentShadow.SetActive(false);
            Destroy(currentShadow);
        }

        // 不指定父对象：分身不会成为 Player 或 Map 的子对象。
        currentShadow = Instantiate(
            shadowPrefab,
            transform.position,
            transform.rotation
        );

        currentShadow.name = "Shadow";

        // 分身生成时没有初始速度。
        Rigidbody2D shadowBody =
            currentShadow.GetComponent<Rigidbody2D>();

        if (shadowBody != null)
        {
            shadowBody.linearVelocity = Vector2.zero;
            shadowBody.angularVelocity = 0f;
        }

        // 分身和玩家起初重叠，忽略两者之间的碰撞，避免互相推挤。
        Collider2D[] playerColliders =
            GetComponentsInChildren<Collider2D>();

        Collider2D[] shadowColliders =
            currentShadow.GetComponentsInChildren<Collider2D>();

        foreach (Collider2D playerCollider in playerColliders)
        {
            foreach (Collider2D shadowCollider in shadowColliders)
            {
                Physics2D.IgnoreCollision(
                    playerCollider,
                    shadowCollider,
                    true
                );
            }
        }
    }

    private void OnDisable()
    {
        if (hint != null) Destroy(hint.gameObject);
        if (mobileButton != null) Destroy(mobileButton.gameObject);
        if (currentShadow != null)
        {
            currentShadow.SetActive(false);
            Destroy(currentShadow);
        }
    }
}
