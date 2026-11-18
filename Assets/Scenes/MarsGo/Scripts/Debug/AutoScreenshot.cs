using System;
using System.Collections;
using System.IO;
using UnityEngine;

namespace MarsGo
{
    /// <summary>
    /// Development helper. Launch the player with  -mg-shots &lt;folder&gt;  and it visits every
    /// level, flies a short scripted hop, then parks the rocket next to each obstacle, checks the
    /// fuel pickup and the laser kill, saves screenshots and quits. Does nothing otherwise.
    /// </summary>
    public class AutoScreenshot : MonoBehaviour
    {
        string _dir;

        void Start()
        {
            var args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
                if (args[i] == "-mg-shots") _dir = args[i + 1];
            if (string.IsNullOrEmpty(_dir)) return;
            SaveData.ReadOnly = true;
            Directory.CreateDirectory(_dir);
            StartCoroutine(Run());
        }

        IEnumerator Shot(string name)
        {
            yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot(Path.Combine(_dir, name + ".png"));
            yield return null;
        }

        static void Log(string msg) => Debug.Log("[mg-shots] " + msg);

        /// <summary>Freezes the rocket in place (kinematic) so the camera can look around.</summary>
        static void Park(Rocket rocket, Vector3 pos)
        {
            rocket.Body.isKinematic = true;
            rocket.Body.position = pos;
            rocket.transform.position = pos;
        }

        IEnumerator Run()
        {
            var gm = GameManager.Instance;
            yield return new WaitForSeconds(0.5f);

            // 1) Every level: start + a short hop.
            for (int i = 0; i < LevelLibrary.Count; i++)
            {
                gm.LoadLevel(i);
                yield return new WaitForSeconds(1.2f);
                yield return Shot($"level{i + 1}_start");
                GameInput.VirtualThrust = 1f;
                yield return new WaitForSeconds(0.9f);
                GameInput.VirtualRotate = -0.5f;
                yield return new WaitForSeconds(0.3f);
                GameInput.VirtualRotate = 0f;
                yield return Shot($"level{i + 1}_thrust");
                GameInput.VirtualThrust = 0f;
                yield return new WaitForSeconds(2.5f);
                Log($"level {i + 1}: state={gm.State} reason={gm.LastCrash.Reason}");
            }

            // 2) Level 3: moving block + fuel capsule.
            gm.LoadLevel(2);
            yield return null;
            Park(gm.Current.Rocket, new Vector3(14f, 6f, 0f));
            yield return new WaitForSeconds(2.2f);
            yield return Shot("obst_block_a");
            yield return new WaitForSeconds(1.4f);
            yield return Shot("obst_block_b");
            GameInput.VirtualThrust = 1f;            // burn some fuel while parked
            yield return new WaitForSeconds(3f);
            GameInput.VirtualThrust = 0f;
            yield return new WaitForSeconds(0.3f);
            float before = gm.Current.Tank.Fuel;
            gm.Current.Rocket.Body.position = new Vector3(25.5f, 8f, 0f);
            yield return new WaitForSeconds(0.15f);
            yield return Shot("obst_fuel_suck");
            yield return new WaitForSeconds(0.5f);
            Log($"fuel capsule: fuel {before:0.0} -> {gm.Current.Tank.Fuel:0.0} (expected +20, capped at max {gm.Current.Tank.fuelMax})");

            // 3) Level 4: wind over the pit.
            gm.LoadLevel(3);
            yield return null;
            Park(gm.Current.Rocket, new Vector3(22f, 11f, 0f));
            yield return new WaitForSeconds(1.5f);
            yield return Shot("obst_wind");
            gm.Current.Rocket.Body.isKinematic = false;
            gm.Current.Rocket.Body.useGravity = false;
            yield return new WaitForSeconds(1.0f);
            Log($"wind: rocket vx after 1 s in tailwind = {gm.Current.Rocket.Body.linearVelocity.x:0.00} m/s (expected ≈ +5)");

            // 4) Level 5: laser + blade.
            gm.LoadLevel(4);
            yield return null;
            Park(gm.Current.Rocket, new Vector3(5f, 6.5f, 0f));
            yield return new WaitForSeconds(1.0f);
            yield return Shot("obst_laser_a");
            yield return new WaitForSeconds(1.2f);
            yield return Shot("obst_laser_b");
            gm.Current.Rocket.Body.position = new Vector3(8.6f, 6.5f, 0f);
            yield return new WaitForSeconds(3.6f);
            Log($"laser: state={gm.State} reason={gm.LastCrash.Reason} (expected Crashed / laser)");

            gm.LoadLevel(4);
            yield return null;
            Park(gm.Current.Rocket, new Vector3(37.5f, 14.5f, 0f));
            yield return new WaitForSeconds(1.5f);
            yield return Shot("obst_blade");
            gm.Current.Rocket.Body.isKinematic = false;
            gm.Current.Rocket.Body.useGravity = false;
            gm.Current.Rocket.Body.linearVelocity = new Vector3(0f, 2f, 0f);
            yield return new WaitForSeconds(2.5f);
            Log($"blade: state={gm.State} reason={gm.LastCrash.Reason} (expected Crashed / blade)");

            // 5) Landing celebration: set the rocket gently down on the level-1 pad.
            gm.LoadLevel(0);
            yield return null;
            var rk = gm.Current.Rocket;
            var pad = gm.Current.LandingPad;
            rk.Body.position = new Vector3(pad.transform.position.x, pad.TopY + 1.8f, 0f);
            rk.transform.position = rk.Body.position;
            rk.Body.linearVelocity = Vector3.zero;
            float waited = 0f;
            while (gm.State == GameState.Playing && waited < 4f) { waited += Time.unscaledDeltaTime; yield return null; }
            Log($"landing: state={gm.State} stars={gm.LastResult.Stars}");
            yield return new WaitForSecondsRealtime(0.25f);
            yield return Shot("land_a");
            yield return new WaitForSecondsRealtime(0.8f);
            yield return Shot("land_b");
            yield return new WaitForSecondsRealtime(1.8f);
            yield return Shot("land_results");

            // 6) Crash: slam into the ground.
            gm.LoadLevel(0);
            yield return null;
            rk = gm.Current.Rocket;
            rk.Body.position = new Vector3(12f, 7f, 0f);
            rk.transform.position = rk.Body.position;
            rk.Body.linearVelocity = new Vector3(2f, -12f, 0f);
            waited = 0f;
            while (gm.State == GameState.Playing && waited < 4f) { waited += Time.unscaledDeltaTime; yield return null; }
            Log($"crash: state={gm.State} reason={gm.LastCrash.Reason} speed={gm.LastCrash.ImpactSpeed:0.0}");
            yield return new WaitForSecondsRealtime(0.15f);
            yield return Shot("crash_a");
            yield return new WaitForSecondsRealtime(0.5f);
            yield return Shot("crash_b");
            yield return new WaitForSecondsRealtime(1.5f);
            yield return Shot("crash_c");

            Application.Quit();
        }
    }
}
