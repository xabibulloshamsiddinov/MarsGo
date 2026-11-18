using UnityEngine;

namespace MarsGo
{
    /// <summary>What a collider means to the LandingJudge (the Godot gameplay groups).</summary>
    public enum SurfaceKind { Ground, Obstacle, Hazard, Friendly, Finish }

    public class SurfaceTag : MonoBehaviour
    {
        public SurfaceKind kind = SurfaceKind.Obstacle;
        [Tooltip("Optional specific crash reason (laser, blade, crushed). Empty = from kind.")]
        public string crashReason = "";

        Collider _collider;

        /// <summary>Top of the surface in world space (pads use it to detect side hits).</summary>
        public float TopY
        {
            get
            {
                if (_collider == null) _collider = GetComponent<Collider>();
                return _collider != null ? _collider.bounds.max.y : transform.position.y;
            }
        }
    }
}
