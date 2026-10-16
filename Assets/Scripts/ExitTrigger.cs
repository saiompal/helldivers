using UnityEngine;

// Level 2 shows the goal during the map preview. It remains locked, dimmed
// red, until every star is collected. Other levels keep their preview rule.
public class ExitTrigger : MonoBehaviour
{
    [SerializeField] private Color lockedColor = new Color(0.45f, 0.12f, 0.1f);
    [SerializeField] private Color lockedGlowColor = new Color(1f, 0.3f, 0.25f);

    private SpriteRenderer spriteRenderer;
    private PulsingGlow glow;
    private Color unlockedColor;
    private Color unlockedGlowColor;

    private void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        glow = GetComponent<PulsingGlow>();

        if (spriteRenderer != null)
        {
            unlockedColor = spriteRenderer.color;
        }

        if (glow != null)
        {
            unlockedGlowColor = glow.GlowColor;
        }
    }

    private void Start()
    {
        GameManager manager = GameManager.Instance;

        if (manager == null || manager.IsExitUnlocked)
        {
            return;
        }

        ShowLocked(true);
        manager.ExitUnlocked += OnExitUnlocked;
    }

    private void Update()
    {
        GameManager manager = GameManager.Instance;
        if (spriteRenderer != null && manager != null)
        {
            spriteRenderer.enabled = manager.IsLevel2 || !manager.IsPreviewing;
        }
    }

    private void OnExitUnlocked()
    {
        ShowLocked(false);
    }

    private void ShowLocked(bool locked)
    {
        if (spriteRenderer != null)
        {
            spriteRenderer.color = locked ? lockedColor : unlockedColor;
        }

        if (glow != null)
        {
            glow.GlowColor = locked ? lockedGlowColor : unlockedGlowColor;
        }
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
}
