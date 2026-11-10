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
        private const int Version = 1;

        public int GetVersion() => Version;

        public JToken Capture(Body body)
        {
            if (!body) return null;
            var loadout = MechLoadoutStore.Get(body);
            var json = loadout.ToJson();
            json["equippedAtUtc"] = DateTime.UtcNow.ToString("o");
            return json;
        }

        public void Restore(Body body, JToken payload, int version, SaveRestoreContext context)
        {
            if (!body || payload == null) return;
            try
            {
                var loadout = MechLoadout.FromJson(payload, out int schema, out string error);
                MechLoadoutStore.Set(body, loadout);
                if (!string.IsNullOrEmpty(error))
                    Plugin.LogInfo("LOADOUT_RESTORE fallback schema=" + schema + " reason=" + error);
                else
                    Plugin.LogInfo("LOADOUT_RESTORE " + loadout.Summary + " palette=" + loadout.PaletteId);
            }
            catch (Exception ex)
            {
                MechLoadoutStore.Set(body, MechLoadout.Default());
                Plugin.LogError("LOADOUT_RESTORE_EXCEPTION " + ex);
            }
        }
    }
}
