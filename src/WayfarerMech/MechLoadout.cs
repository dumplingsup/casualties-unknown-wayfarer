using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace WayfarerMech
{
    /// <summary>
    /// Per-character loadout. It is deliberately not a global config: two saves must be
    /// able to keep different bodies, arms and palettes.
    /// </summary>
    [Serializable]
    public sealed class MechLoadout
    {
        public const int SchemaVersion = 1;

        public string BodyId = MechCatalog.DefaultBody;
        public string LeftArmId = MechCatalog.DefaultLeftArm;
        public string RightArmId = MechCatalog.DefaultRightArm;
        public string LegsId = MechCatalog.DefaultLegs;
        public string BoosterId = MechCatalog.DefaultBooster;
        public string HornId = MechCatalog.DefaultHorn;
        public string PaletteId = MechCatalog.DefaultPalette;

        public static MechLoadout Default()
        {
            return new MechLoadout();
        }

        public MechLoadout Clone()
        {
            return new MechLoadout
            {
                BodyId = BodyId,
                LeftArmId = LeftArmId,
                RightArmId = RightArmId,
                LegsId = LegsId,
                BoosterId = BoosterId,
                HornId = HornId,
                PaletteId = PaletteId,
            };
        }

        public string Get(MechSlot slot)
        {
            switch (slot)
            {
                case MechSlot.Body: return BodyId;
                case MechSlot.ArmLeft: return LeftArmId;
                case MechSlot.ArmRight: return RightArmId;
                case MechSlot.Legs: return LegsId;
                case MechSlot.Booster: return BoosterId;
                case MechSlot.Horn: return HornId;
            }
            return null;
        }

        public void Set(MechSlot slot, string id)
        {
            switch (slot)
            {
                case MechSlot.Body: BodyId = id; break;
                case MechSlot.ArmLeft: LeftArmId = id; break;
                case MechSlot.ArmRight: RightArmId = id; break;
                case MechSlot.Legs: LegsId = id; break;
                case MechSlot.Booster: BoosterId = id; break;
                case MechSlot.Horn: HornId = id; break;
            }
        }

        /// <summary>Normalise unknown IDs and missing core parts to the preinstalled set.</summary>
        public bool Normalize(out string reason)
        {
            reason = null;
            if (Array.IndexOf(MechCatalog.Palettes, PaletteId) < 0)
                PaletteId = MechCatalog.DefaultPalette;

            var body = MechCatalog.Get(MechSlot.Body, BodyId);
            if (body == null) { BodyId = MechCatalog.DefaultBody; reason = "缺机体，已使用预装 Champion-00"; }
            var legs = MechCatalog.Get(MechSlot.Legs, LegsId);
            if (legs == null) { LegsId = MechCatalog.DefaultLegs; reason = "缺腿部件，已使用预装巡行者腿"; }
            var booster = MechCatalog.Get(MechSlot.Booster, BoosterId);
            if (booster == null) { BoosterId = MechCatalog.DefaultBooster; reason = "缺推进器，已使用预装矢量推进器"; }

            if (!string.IsNullOrEmpty(LeftArmId) && MechCatalog.GetArm(LeftArmId) == null)
            { LeftArmId = MechCatalog.DefaultLeftArm; reason = "缺左臂部件，已使用机械钻"; }
            if (!string.IsNullOrEmpty(RightArmId) && MechCatalog.GetArm(RightArmId) == null)
            { RightArmId = MechCatalog.DefaultRightArm; reason = "缺右臂部件，已使用机炮"; }
            if (string.IsNullOrEmpty(LeftArmId)) LeftArmId = MechCatalog.DefaultLeftArm;
            if (string.IsNullOrEmpty(RightArmId)) RightArmId = MechCatalog.DefaultRightArm;
            if (MechCatalog.Get(MechSlot.Horn, HornId) == null) HornId = MechCatalog.DefaultHorn;
            return reason == null;
        }

        public JObject ToJson()
        {
            return new JObject
            {
                ["schemaVersion"] = SchemaVersion,
                ["body"] = BodyId,
                ["leftArm"] = LeftArmId,
                ["rightArm"] = RightArmId,
                ["legs"] = LegsId,
                ["booster"] = BoosterId,
                ["horn"] = HornId,
                ["palette"] = PaletteId,
            };
        }

        public static MechLoadout FromJson(JToken token, out int schemaVersion, out string error)
        {
            schemaVersion = 0;
            error = null;
            var obj = token as JObject;
            if (obj == null) { error = "loadout payload is not an object"; return Default(); }
            schemaVersion = obj.Value<int?>("schemaVersion") ?? 0;
            // A newer schema is not silently down-converted. The caller keeps this as a
            // diagnostic and uses the preinstalled set; the raw save data is not overwritten
            // unless the player explicitly applies a new loadout later.
            if (schemaVersion > SchemaVersion)
            {
                error = "loadout schema " + schemaVersion + " is newer than this build";
                return Default();
            }
            var l = new MechLoadout
            {
                BodyId = obj.Value<string>("body") ?? MechCatalog.DefaultBody,
                LeftArmId = obj.Value<string>("leftArm") ?? MechCatalog.DefaultLeftArm,
                RightArmId = obj.Value<string>("rightArm") ?? MechCatalog.DefaultRightArm,
                LegsId = obj.Value<string>("legs") ?? MechCatalog.DefaultLegs,
                BoosterId = obj.Value<string>("booster") ?? MechCatalog.DefaultBooster,
                HornId = obj.Value<string>("horn") ?? MechCatalog.DefaultHorn,
                PaletteId = obj.Value<string>("palette") ?? MechCatalog.DefaultPalette,
            };
            if (l.Normalize(out var reason)) { }
            else
            {
                error = reason;
            }
            return l;
        }

        public static MechLoadout FromJson(JToken token) => FromJson(token, out _, out _);

        public string Summary
        {
            get
            {
                var stats = MechCatalog.Combine(this);
                string b = stats.Body != null ? stats.Body.Name : "?";
                string l = stats.LeftArm != null ? stats.LeftArm.Name : "空";
                string r = stats.RightArm != null ? stats.RightArm.Name : "空";
                string g = stats.Legs != null ? stats.Legs.Name : "?";
                string p = stats.Booster != null ? stats.Booster.Name : "?";
                return b + " / 左:" + l + " / 右:" + r + " / " + g + " / " + p;
            }
        }
    }

    /// <summary>Per-body loadout store, restored by CUCoreLib's body save provider.</summary>
    public static class MechLoadoutStore
    {
        private sealed class Holder { public MechLoadout Value; public PreservedLoadoutRecord Original; }
        private static readonly ConditionalWeakTable<Body, Holder> table = new ConditionalWeakTable<Body, Holder>();

        public static MechLoadout Get(Body body)
        {
            if (!body) return MechLoadout.Default();
            return table.GetValue(body, _ => new Holder { Value = MechLoadout.Default() }).Value;
        }

        public static void Set(Body body, MechLoadout loadout)
        {
            if (!body || loadout == null) return;
            table.Remove(body);
            table.Add(body, new Holder { Value = loadout });
        }

        public static void Ensure(Body body)
        {
            if (!body) return;
            Get(body);
        }

        public static void Restore(Body body, MechLoadout loadout, JToken payload, int version)
        {
            Set(body, loadout);
            if (body && table.TryGetValue(body, out var holder))
                holder.Original = new PreservedLoadoutRecord(payload, version);
        }

        public static JToken Capture(Body body, out int version)
        {
            version = 1;
            if (!body) return null;
            Get(body);
            var holder = table.GetValue(body, _ => new Holder { Value = MechLoadout.Default() });
            if (holder.Original != null)
            {
                version = holder.Original.Version;
                return holder.Original.Capture();
            }
            return holder.Value.ToJson();
        }
    }
}
