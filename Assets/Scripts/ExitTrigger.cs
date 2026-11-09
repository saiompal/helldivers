using UnityEngine;

// Every level shows the goal as a bright red landmark during memorization.
// During play it remains locked and dim red until every star is collected.
public class ExitTrigger : MonoBehaviour
{
    [SerializeField] private Color lockedColor = new Color(0.45f, 0.12f, 0.1f);
    [SerializeField] private Color lockedGlowColor = new Color(1f, 0.3f, 0.25f);
    [SerializeField] private Color previewColor = new Color(1f, 0.04f, 0.03f);
    [SerializeField] private Color previewGlowColor = new Color(1f, 0.08f, 0.03f);

    private SpriteRenderer spriteRenderer;
    private PulsingGlow glow;
    private Color unlockedColor;
    private Color unlockedGlowColor;
    private bool wasPreviewing;

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
        if (manager == null)
            return;

        if (manager.IsPreviewing)
        {
            wasPreviewing = true;
            if (spriteRenderer != null)
            {
                spriteRenderer.enabled = true;
                spriteRenderer.color = previewColor;
            }
            if (glow != null)
                glow.GlowColor = previewGlowColor;
        }
        else if (wasPreviewing)
        {
            wasPreviewing = false;
            ShowLocked(!manager.IsExitUnlocked);
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
