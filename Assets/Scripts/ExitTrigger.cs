using UnityEngine;

// Every level shows the goal as a bright red landmark during memorization.
// During play it remains locked and dim red until every star is collected.
public class ExitTrigger : MonoBehaviour
{
    [SerializeField] private Sprite lockedDoor;
    [SerializeField] private Sprite unlockedDoor;
    [SerializeField] private UnityEngine.Rendering.Universal.Light2D unlockedLight;

    private SpriteRenderer spriteRenderer;
    private PulsingGlow glow;
    private GameManager manager;
    private bool wasPreviewing;

    private void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        glow = GetComponent<PulsingGlow>();
    }

    private void Start()
    {
        manager = GameManager.Instance;

        if (manager == null)
        {
            return;
        }

        manager.ExitUnlocked += OnExitUnlocked;
        RefreshDoor();
    }

    private void Update()
    {
        if (manager != null && wasPreviewing != manager.IsPreviewing)
            RefreshDoor();
    }

    private void OnExitUnlocked()
    {
        RefreshDoor();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            GameManager.Instance?.TryExit();
        }
    }

    private void OnDestroy()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.ExitUnlocked -= OnExitUnlocked;
        }
    }
    private void RefreshDoor()
    {
        wasPreviewing = manager.IsPreviewing;
        bool showOpenDoor = manager.IsExitUnlocked && !wasPreviewing;

        if (spriteRenderer != null)
        {
            Sprite doorSprite = showOpenDoor ? unlockedDoor : lockedDoor;
            if (doorSprite != null)
                spriteRenderer.sprite = doorSprite;

            spriteRenderer.color = Color.white;

            if (wasPreviewing)
                spriteRenderer.enabled = true;
        }

        if (unlockedLight != null)
        {
            unlockedLight.enabled = showOpenDoor;
        }
    }
}
