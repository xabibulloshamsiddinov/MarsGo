using UnityEngine;

namespace MarsGo
{
    /// <summary>Trigger below the level: a rocket that falls in crashes.</summary>
    [RequireComponent(typeof(BoxCollider))]
    public class DeathZone : MonoBehaviour
    {
        public string crashReason = CrashReasons.Hazard;

        void OnTriggerEnter(Collider other)
        {
            var judge = other.GetComponentInParent<LandingJudge>();
            if (judge == null) return;
            judge.ForceCrash(crashReason, judge.Rocket.Body.linearVelocity.magnitude);
        }
    }
}
