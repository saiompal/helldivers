using UnityEngine;

public class StarCollectible : MonoBehaviour
{
    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag("Player"))
        {
            return;
        }

        if (GameManager.Instance != null && GameManager.Instance.TryCollectStar())
        {
            SpriteRenderer appearance = GetComponent<SpriteRenderer>();
            if (appearance != null)
            {
                StarPickupEffect.Play(
                    transform.position, appearance.sprite, appearance.sharedMaterial);
            }

            Destroy(gameObject);
        }
    }
}
