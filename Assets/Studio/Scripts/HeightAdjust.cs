using UnityEngine;

namespace Studio
{
    /// <summary>Höhenverstellbarer Teil eines Objekts (z. B. Bildschirm am Standfuß).</summary>
    public class HeightAdjust : MonoBehaviour
    {
        public Transform target;
        public float height, minHeight, maxHeight;
        public string label = "Höhe";

        public void Apply()
        {
            height = Mathf.Clamp(height, minHeight, maxHeight);
            diagonal = Mathf.Clamp(diagonal, minDiagonal, maxDiagonal);
            if (target != null) target.localPosition = new Vector3(target.localPosition.x, height, target.localPosition.z);
            if (sizeTarget != null) sizeTarget.localScale = Vector3.one * (diagonal / baseDiagonal);
        }

        // ---- optional: Bildschirmdiagonale in Zoll (Fernseher) ----
        public Transform sizeTarget;
        public float diagonal, baseDiagonal = 1f, minDiagonal = 32f, maxDiagonal = 98f;
        public bool HasSize => sizeTarget != null;

        /// <summary>Sichtbare Bildbreite × -höhe in Metern (16:9) für die Anzeige.</summary>
        public Vector2 ScreenMeters => new Vector2(diagonal * 0.0254f * 16f / 18.3576f, diagonal * 0.0254f * 9f / 18.3576f);
    }
}
