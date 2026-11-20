using UnityEngine;
using UnityEngine.InputSystem;

public class ShadowCloneSpawner : MonoBehaviour
{
    [SerializeField] private GameObject shadowPrefab;

    private GameObject currentShadow;

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
            CreateShadow();
        }
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
        if (currentShadow != null)
        {
            currentShadow.SetActive(false);
            Destroy(currentShadow);
        }
    }
}