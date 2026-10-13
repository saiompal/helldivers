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
            Destroy(gameObject);
        }
    }
}
