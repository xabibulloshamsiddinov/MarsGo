using System.Collections.Generic;

namespace MarsGo
{
    public enum AudioBus { Sfx, UI, Ambience, Music }

    /// <summary>
    /// Every named sound (port of audio/sfx_library.tres): variants, volume, pitch variation, bus.
    /// Clips live in Resources/MarsGo/Audio/{Sfx,Ambience}.
    /// </summary>
    public static class SfxLibrary
    {
        public class Entry
        {
            public string Id;
            public string[] Clips;
            public float VolumeDb;
            public float PitchVariation;
            public AudioBus Bus;
        }

        static Dictionary<string, Entry> _entries;

        static void Add(string id, float db, float pitchVar, params string[] clips) => Add(id, db, pitchVar, AudioBus.Sfx, clips);

        static void Add(string id, float db, float pitchVar, AudioBus bus, params string[] clips)
        {
            _entries[id] = new Entry { Id = id, Clips = clips, VolumeDb = db, PitchVariation = pitchVar, Bus = bus };
        }

        public static Entry Get(string id)
        {
            if (_entries == null) Build();
            return _entries.TryGetValue(id, out var e) ? e : null;
        }

        static void Build()
        {
            _entries = new Dictionary<string, Entry>();
            Add("engine_loop", 0f, 0f, "Sfx/engine_loop");
            Add("engine_ignition", -3f, 0.08f, "Sfx/engine_ignition");
            Add("engine_shutdown", -6f, 0.08f, "Sfx/engine_shutdown");
            Add("engine_sputter", -4f, 0.1f, "Sfx/engine_sputter_1", "Sfx/engine_sputter_2");
            Add("rcs_puff", -10f, 0.12f, "Sfx/rcs_puff_1", "Sfx/rcs_puff_2", "Sfx/rcs_puff_3");
            Add("explosion_impact", 0f, 0.08f, "Sfx/explosion_impact_1", "Sfx/explosion_impact_2", "Sfx/explosion_impact_3");
            Add("explosion_rumble", -2f, 0.05f, "Sfx/explosion_rumble_1", "Sfx/explosion_rumble_2");
            Add("explosion_debris", -6f, 0.1f, "Sfx/explosion_debris_1", "Sfx/explosion_debris_2", "Sfx/explosion_debris_3");
            Add("land_soft", -4f, 0.06f, "Sfx/land_soft_1", "Sfx/land_soft_2");
            Add("land_hard", -1f, 0.06f, "Sfx/land_hard_1", "Sfx/land_hard_2");
            Add("star_pop_1", -4f, 0f, "Sfx/star_pop_1");
            Add("star_pop_2", -4f, 0f, "Sfx/star_pop_2");
            Add("star_pop_3", -4f, 0f, "Sfx/star_pop_3");
            Add("fuel_pickup", -3f, 0.03f, "Sfx/fuel_pickup_1", "Sfx/fuel_pickup_2");
            Add("fuel_low_beep", -6f, 0f, AudioBus.UI, "Sfx/fuel_low_beep");
            Add("hazard_warning", -8f, 0f, "Sfx/hazard_warning");
            Add("laser_hum", -12f, 0f, "Sfx/laser_hum");
            Add("laser_zap", -5f, 0.1f, "Sfx/laser_zap_1", "Sfx/laser_zap_2");
            Add("laser_charge", -8f, 0f, "Sfx/laser_charge");
            Add("laser_off", -10f, 0.05f, "Sfx/laser_off");
            Add("block_slam", -4f, 0.1f, "Sfx/block_slam_1", "Sfx/block_slam_2");
            Add("blade_whoosh", -6f, 0.12f, "Sfx/blade_whoosh_1", "Sfx/blade_whoosh_2");
            Add("wind_gust", -6f, 0.1f, "Sfx/wind_gust_1", "Sfx/wind_gust_2");
            Add("wind_zone_loop", -10f, 0f, "Sfx/wind_zone_loop");
            Add("meteor_whoosh", -4f, 0.1f, "Sfx/meteor_whoosh_1", "Sfx/meteor_whoosh_2");
            Add("meteor_impact", -2f, 0.1f, "Sfx/meteor_impact_1", "Sfx/meteor_impact_2");
            Add("distant_boom", -6f, 0.15f, AudioBus.Ambience, "Ambience/distant_boom_1", "Ambience/distant_boom_2", "Ambience/distant_boom_3");
            Add("ui_hover", -10f, 0.04f, AudioBus.UI, "Sfx/ui_hover_1", "Sfx/ui_hover_2");
            Add("ui_click", -6f, 0.04f, AudioBus.UI, "Sfx/ui_click_1", "Sfx/ui_click_2");
            Add("ui_back", -6f, 0.02f, AudioBus.UI, "Sfx/ui_back");
            Add("ui_pause", -6f, 0f, AudioBus.UI, "Sfx/ui_pause");
            Add("ui_level_start", -4f, 0f, AudioBus.UI, "Sfx/ui_level_start");
        }
    }
}
