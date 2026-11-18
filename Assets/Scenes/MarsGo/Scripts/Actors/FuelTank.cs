using System;
using UnityEngine;

namespace MarsGo
{
    /// <summary>Fuel component (port of fuel_tank.gd). Burns fuel by throttle; at zero the engine stops.</summary>
    [RequireComponent(typeof(Rocket))]
    public class FuelTank : MonoBehaviour
    {
        public event Action<float, float> FuelChanged;
        /// <summary>Once per life when fuel drops below lowFuelRatio.</summary>
        public event Action FuelLow;
        public event Action FuelEmpty;
        /// <summary>A pickup added fuel (amount that actually went in).</summary>
        public event Action<float> FuelAdded;

        [Tooltip("Overridden by LevelDef.FuelCapacity when the level starts.")]
        public float fuelMax = 100f;
        [Tooltip("Units per second at full throttle.")]
        public float burnRate = 8f;
        [Range(0f, 1f)] public float lowFuelRatio = 0.25f;

        public float Fuel { get; private set; }

        Rocket _rocket;
        float _throttle;
        bool _lowSent, _emptySent;

        void Awake()
        {
            _rocket = GetComponent<Rocket>();
            _rocket.ThrustChanged += amount => _throttle = amount;
        }

        void FixedUpdate()
        {
            if (_throttle <= 0f || Fuel <= 0f) return;
            SetFuel(Fuel - burnRate * _throttle * Time.fixedDeltaTime);
        }

        /// <summary>Fills the tank. newMax > 0 changes the capacity.</summary>
        public void Refill(float newMax = -1f)
        {
            if (newMax > 0f) fuelMax = newMax;
            _lowSent = false;
            _emptySent = false;
            _rocket.SetThrustAvailable(true);
            SetFuel(fuelMax);
        }

        public void AddFuel(float amount)
        {
            if (amount <= 0f) return;
            float before = Fuel;
            SetFuel(Fuel + amount);
            if (Ratio >= lowFuelRatio) _lowSent = false;
            if (Fuel > 0f)
            {
                _emptySent = false;
                _rocket.SetThrustAvailable(true);
            }
            FuelAdded?.Invoke(Fuel - before);
        }

        public float Ratio => fuelMax > 0f ? Fuel / fuelMax : 0f;

        public bool IsEmpty => Fuel <= 0f;

        void SetFuel(float value)
        {
            Fuel = Mathf.Clamp(value, 0f, fuelMax);
            FuelChanged?.Invoke(Fuel, fuelMax);
            if (!_lowSent && Ratio < lowFuelRatio && Fuel > 0f)
            {
                _lowSent = true;
                FuelLow?.Invoke();
            }
            if (!_emptySent && Fuel <= 0f)
            {
                _emptySent = true;
                _rocket.SetThrustAvailable(false);
                FuelEmpty?.Invoke();
            }
        }
    }
}
