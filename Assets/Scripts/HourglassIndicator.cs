using UnityEngine;

public class HourglassIndicator : MonoBehaviour
{
    [SerializeField] private MapFlipController mapFlip;
    [SerializeField] private HourglassGraphic graphic;
    public void Configure(MapFlipController controller, HourglassGraphic artwork)
    { mapFlip = controller; graphic = artwork; }
    private void Update()
    {
        if (mapFlip == null || graphic == null) return;
        if (GameManager.Instance != null && GameManager.Instance.IsFinished) return;
        bool warning = mapFlip.CurrentPhase == MapFlipController.Phase.Warning;
        bool turning = mapFlip.IsTurning;
        float pulse = warning ? .3f + .7f * (Mathf.Sin(Time.time * 10) + 1) * .5f : 0;
        graphic.SetState(mapFlip.CycleProgress01, pulse);
        transform.localRotation = Quaternion.Euler(0, 0, -180 * mapFlip.TurnProgress01);
        float scale = turning ? 1f + .12f * Mathf.Sin(mapFlip.TurnProgress01 * Mathf.PI)
            : warning ? 1f + .045f * pulse : 1f;
        transform.localScale = Vector3.one * scale;
    }
}
