using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class ShadowFlipFollower : MonoBehaviour
{
    private Rigidbody2D body;
    private MapFlipController mapFlip;

    private Vector3 turnStartPosition;
    private RigidbodyInterpolation2D savedInterpolation;
    private bool turning;

    private void Awake()
    {
        body = GetComponent<Rigidbody2D>();
    }

    private void OnEnable()
    {
        // 找到与这个分身处于同一场景的地图控制器。
        foreach (MapFlipController candidate in
                 FindObjectsByType<MapFlipController>(
                     FindObjectsSortMode.None))
        {
            if (candidate.gameObject.scene == gameObject.scene)
            {
                mapFlip = candidate;
                break;
            }
        }

        if (mapFlip == null)
        {
            Debug.LogWarning("分身找不到 MapFlipController。", this);
            return;
        }

        mapFlip.TurnStarted += BeginTurn;
        mapFlip.TurnPoseApplied += ApplyTurn;
        mapFlip.TurnFinished += FinishTurn;
    }

    private void BeginTurn()
    {
        turning = true;

        // 每次翻转前重新记录分身的位置。
        turnStartPosition = transform.position;
        savedInterpolation = body.interpolation;

        body.linearVelocity = Vector2.zero;
        body.angularVelocity = 0f;
        body.interpolation = RigidbodyInterpolation2D.None;

        // 防止旋转中的墙壁把分身挤走。
        body.simulated = false;
    }

    private void ApplyTurn(Vector3 pivot, Quaternion rotation)
    {
        if (!turning)
            return;

        Vector3 offset = turnStartPosition - pivot;
        offset.z = 0f;

        Vector3 position = pivot + rotation * offset;

        transform.position = new Vector3(
            position.x,
            position.y,
            turnStartPosition.z
        );
    }

    private void FinishTurn()
    {
        if (!turning)
            return;

        // 同步物理位置，再恢复重力模拟。
        body.position = (Vector2)transform.position;
        body.simulated = true;
        body.interpolation = savedInterpolation;

        body.linearVelocity = Vector2.zero;
        body.angularVelocity = 0f;

        // 恢复物理后，再次设置分身与玩家互不碰撞。
        IgnorePlayerCollision();

        turning = false;
    }

    private void IgnorePlayerCollision()
    {
        if (mapFlip == null || mapFlip.PlayerTransform == null)
            return;

        Collider2D[] shadowColliders =
            GetComponentsInChildren<Collider2D>();

        Collider2D[] playerColliders =
            mapFlip.PlayerTransform.GetComponentsInChildren<Collider2D>();

        foreach (Collider2D shadowCollider in shadowColliders)
        {
            foreach (Collider2D playerCollider in playerColliders)
            {
                Physics2D.IgnoreCollision(
                    shadowCollider,
                    playerCollider,
                    true
                );
            }
        }
    }

    private void OnDisable()
    {
        if (mapFlip != null)
        {
            mapFlip.TurnStarted -= BeginTurn;
            mapFlip.TurnPoseApplied -= ApplyTurn;
            mapFlip.TurnFinished -= FinishTurn;
        }

        // 避免翻转过程中禁用组件后，刚体一直停用。
        if (turning && body != null)
        {
            body.position = (Vector2)transform.position;
            body.simulated = true;
            body.interpolation = savedInterpolation;
            turning = false;
        }
    }
}