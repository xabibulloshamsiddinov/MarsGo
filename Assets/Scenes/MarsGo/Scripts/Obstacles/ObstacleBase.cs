using System;
using UnityEngine;

namespace MarsGo
{
    /// <summary>
    /// Shared contract for obstacles (the Godot "AudioEmitter" group): every obstacle has an
    /// audio kind and raises warning / activated / deactivated / collected events.
    /// Audio (Phase 3) subscribes through ObstacleBase.AnySignal; gameplay never plays sounds.
    /// </summary>
    public abstract class ObstacleBase : MonoBehaviour
    {
        public const string SignalWarning = "warning";
        public const string SignalActivated = "activated";
        public const string SignalDeactivated = "deactivated";
        public const string SignalCollected = "collected";

        /// <summary>Raised by every obstacle in the scene: (obstacle, signal name).</summary>
        public static event Action<ObstacleBase, string> AnySignal;

        public event Action<string> Signal;

        public abstract string AudioKind { get; }

        protected void Emit(string signal)
        {
            Signal?.Invoke(signal);
            AnySignal?.Invoke(this, signal);
        }

        /// <summary>The rocket's judge if the collider belongs to a still-flying rocket.</summary>
        protected static LandingJudge FlyingJudge(Collider other)
        {
            var judge = other.GetComponentInParent<LandingJudge>();
            return judge != null && !judge.IsResolved ? judge : null;
        }

        protected static Rocket FindRocket()
        {
            var gm = GameManager.Instance;
            return gm != null && gm.Current != null ? gm.Current.Rocket : null;
        }
    }
}
