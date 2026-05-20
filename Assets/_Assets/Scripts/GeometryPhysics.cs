using UnityEngine;
using DG.Tweening;

/// <summary>
/// DOTween physics mover for ISAS-based geometry interactions.
/// </summary>
public static class GeometryPhysics
{
    /// <summary>
    /// Spawns with a drop-from-above effect: object starts at startPos with scale 0
    /// and springs to targetPos + targetScale using DOTween.
    /// </summary>
    public static void Spawn(GameObject obj, Vector3 startPos, Vector3 targetPos, Vector3 targetScale)
    {
        if (obj == null) return;
        
        obj.transform.position = startPos;
        obj.transform.localScale = Vector3.zero;

        // Smooth drop and bounce
        obj.transform.DOMove(targetPos, 0.7f).SetEase(Ease.OutBounce);
        // Pop in scale
        obj.transform.DOScale(targetScale, 0.6f).SetEase(Ease.OutBack);
    }

    /// <summary>
    /// Rearrange: object slides from its current position to targetPos with a
    /// smooth magnetic glide for "touching".
    /// </summary>
    public static void Rearrange(GameObject obj, Vector3 targetPos)
    {
        if (obj == null) return;
        
        obj.transform.DOMove(targetPos, 0.8f).SetEase(Ease.InOutCubic);
    }

    /// <summary>
    /// Split / burst: object starts at splitOrigin (scale 0) and is flung outward
    /// toward targetPos with an energetic arc (DOJump).
    /// </summary>
    public static void Split(GameObject obj, Vector3 splitOrigin, Vector3 targetPos, Vector3 targetScale)
    {
        if (obj == null) return;

        obj.transform.position = splitOrigin;
        obj.transform.localScale = Vector3.zero;

        float dist = Vector3.Distance(splitOrigin, targetPos);
        float jumpPower = Mathf.Clamp(dist * 0.5f, 0.5f, 2f);

        obj.transform.DOJump(targetPos, jumpPower, 1, 0.7f).SetEase(Ease.OutQuad);
        obj.transform.DOScale(targetScale, 0.6f).SetEase(Ease.OutBack);
    }
}
