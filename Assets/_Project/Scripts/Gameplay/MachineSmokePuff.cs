using System.Collections;
using UnityEngine;
using FarmFuryArcade.Utilities;

namespace FarmFuryArcade.Gameplay
{
    /// <summary>One fading exhaust puff spawned behind a machine-skinned character while it moves
    /// (Machine Cosmetics, 2026-09-15) — see CosmeticData.spawnsMovementSmoke and
    /// CharacterCosmeticRenderer.SpawnMachineSmokePuff. Grows slightly, drifts upward, and fades
    /// out before self-destroying — same "self-contained, Destroy(gameObject) when done"
    /// convention CosmeticTrailGhost/PelletCollectBurst already use. Procedural placeholder (a
    /// tinted PlaceholderSprite circle) until dedicated exhaust-smoke art exists — swap the sprite
    /// here for real art with no other changes needed.</summary>
    public class MachineSmokePuff : MonoBehaviour
    {
        private static readonly Color SmokeColor = new Color(0.55f, 0.55f, 0.55f, 0.6f);
        private const float StartScale = 0.22f;
        private const float EndScaleMultiplier = 1.9f;
        private const float DriftUpDistance = 0.2f;

        public void Configure(int sortingLayerID, int sortingOrder, float lifetimeSeconds)
        {
            var sr = gameObject.AddComponent<SpriteRenderer>();
            sr.sprite = PlaceholderSprite.GetCircle(SmokeColor);
            sr.color = SmokeColor;
            sr.sortingLayerID = sortingLayerID;
            sr.sortingOrder = sortingOrder;
            transform.localScale = Vector3.one * StartScale;
            StartCoroutine(DriftFadeAndDestroy(sr, lifetimeSeconds));
        }

        private IEnumerator DriftFadeAndDestroy(SpriteRenderer sr, float lifetimeSeconds)
        {
            Vector3 startScale = transform.localScale;
            Vector3 startPos = transform.position;
            float t = 0f;
            while (t < lifetimeSeconds)
            {
                t += Time.deltaTime;
                float progress = Mathf.Clamp01(t / lifetimeSeconds);
                transform.position = startPos + Vector3.up * (DriftUpDistance * progress);
                transform.localScale = Vector3.Lerp(startScale, startScale * EndScaleMultiplier, progress);
                Color c = sr.color;
                c.a = Mathf.Lerp(SmokeColor.a, 0f, progress);
                sr.color = c;
                yield return null;
            }
            Destroy(gameObject);
        }
    }
}
