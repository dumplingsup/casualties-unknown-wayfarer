using BepInEx.Configuration;

namespace WayfarerMech
{
    /// <summary>Central tuning. No gameplay magic numbers should live outside this file.</summary>
    public static class MechSettings
    {
        public static ConfigEntry<float> JumpSpeed;
        public static ConfigEntry<float> HoldThreshold;
        public static ConfigEntry<float> ThrustNetAccel;
        public static ConfigEntry<float> MaxRiseSpeed;
        public static ConfigEntry<float> MaxFallSpeed;
        public static ConfigEntry<float> MoveSpeed;
        public static ConfigEntry<float> HorizontalAccel;

        public static ConfigEntry<float> DrillInterval;
        public static ConfigEntry<float> DrillTerrainDamage;
        public static ConfigEntry<float> DrillEntityDamage;
        public static ConfigEntry<float> DrillAnimalDamage;
        public static ConfigEntry<int> DrillDepth;
        public static ConfigEntry<int> DrillMaxCells;
        public static ConfigEntry<float> DrillRange;
        public static ConfigEntry<float> DrillEntityRange;
        public static ConfigEntry<int> DrillMaxSoftBreaks;

        public static ConfigEntry<float> SafeSpotInterval;

        public static void Bind(ConfigFile config)
        {
            JumpSpeed = config.Bind("Jetpack", "JumpSpeed", 26f, Range("Short jump launch speed (world units/s).", 10f, 50f));
            HoldThreshold = config.Bind("Jetpack", "HoldThreshold", 0.18f, Range("Seconds the jump key must be held before continuous thrust starts.", 0.05f, 1f));
            ThrustNetAccel = config.Bind("Jetpack", "ThrustNetAccel", 35f, Range("Net upward acceleration while thrusting (gravity compensated).", 5f, 120f));
            MaxRiseSpeed = config.Bind("Jetpack", "MaxRiseSpeed", 26f, Range("Maximum upward speed while thrusting.", 5f, 60f));
            MaxFallSpeed = config.Bind("Jetpack", "MaxFallSpeed", 40f, Range("Maximum downward speed while piloting.", 10f, 80f));
            MoveSpeed = config.Bind("Ground", "MoveSpeed", 12f, Range("Horizontal speed target while piloting.", 2f, 30f));
            HorizontalAccel = config.Bind("Ground", "HorizontalAccel", 70f, Range("Horizontal acceleration while piloting.", 10f, 200f));

            DrillInterval = config.Bind("Drill", "Interval", 0.10f, Range("Seconds between drill pulses.", 0.03f, 0.5f));
            DrillTerrainDamage = config.Bind("Drill", "TerrainDamage", 2400f, Range("Damage per pulse to each selected terrain cell. Vanilla drops/health are preserved.", 100f, 20000f));
            DrillEntityDamage = config.Bind("Drill", "EntityDamage", 180f, Range("Damage per drill pulse to a building/entity. Separate from terrain damage.", 10f, 1000f));
            DrillAnimalDamage = config.Bind("Drill", "AnimalDamage", 80f, Range("Drill damage per pulse to animals.", 10f, 1000f));
            DrillDepth = config.Bind("Drill", "MaxDepth", 2, Range("Maximum solid cells per column from the first visible surface.", 1, 4));
            DrillMaxCells = config.Bind("Drill", "MaxCellsPerPulse", 24, Range("Nearest-first cell budget per drill pulse.", 1, 64));
            DrillRange = config.Bind("Drill", "RangeFromCenter", 10f, Range("Maximum drilling distance measured from the mech centre (world units). The ray starts at the drill tip.", 3f, 20f));
            DrillEntityRange = config.Bind("Drill", "EntityRange", 8f, Range("Maximum native raycast distance from the drill tip for buildings/animals.", 2f, 16f));
            DrillMaxSoftBreaks = config.Bind("Drill", "MaxSoftBreaksPerPulse", 8, Range("Effect throttle: one-pulse-breakable cells per pulse. Hard blocks are not limited here; the maxCells budget still applies.", 1, 32));

            SafeSpotInterval = config.Bind("Safety", "SafeSpotInterval", 0.2f, Range("Seconds between grounded safe-spot samples.", 0.05f, 1f));
        }

        private static ConfigDescription Range(string description, float min, float max)
            => new ConfigDescription(description, new AcceptableValueRange<float>(min, max));

        private static ConfigDescription Range(string description, int min, int max)
            => new ConfigDescription(description, new AcceptableValueRange<int>(min, max));
    }
}
