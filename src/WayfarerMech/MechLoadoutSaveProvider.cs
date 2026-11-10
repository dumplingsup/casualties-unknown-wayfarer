using System;
using CUCoreLib.Saving;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace WayfarerMech
{
    /// <summary>
    /// CUCoreLib body-scoped save provider. The loadout belongs to the character/run,
    /// not to the global BepInEx config. Missing or newer data is reported and a playable
    /// default is used; the raw save record is not silently rewritten.
    /// </summary>
    public sealed class MechLoadoutSaveProvider : IBodySaveProvider
    {
        public const string Key = "local.cu.wayfarermech.loadout";
        private int capturedVersion = 1;

        // CUCoreLib 1.1 calls Capture(body), then GetVersion(), synchronously for
        // each body. Preserve the envelope version too, including future records.
        public int GetVersion() => capturedVersion;

        public JToken Capture(Body body)
        {
            return MechLoadoutStore.Capture(body, out capturedVersion);
        }

        public void Restore(Body body, JToken payload, int version, SaveRestoreContext context)
        {
            if (!body || payload == null) return;
            try
            {
                var loadout = MechLoadout.FromJson(payload, out int schema, out string error);
                if (version > 1)
                {
                    loadout = MechLoadout.Default();
                    error = "provider version " + version + " is newer than this build";
                }
                MechLoadoutStore.Restore(body, loadout, payload, version);
                if (!string.IsNullOrEmpty(error))
                    Plugin.LogInfo("LOADOUT_RESTORE fallback schema=" + schema + " reason=" + error);
                else
                    Plugin.LogInfo("LOADOUT_RESTORE " + loadout.Summary + " palette=" + loadout.PaletteId);
            }
            catch (Exception ex)
            {
                MechLoadoutStore.Restore(body, MechLoadout.Default(), payload, version);
                Plugin.LogError("LOADOUT_RESTORE_EXCEPTION " + ex);
            }
        }
    }
}
