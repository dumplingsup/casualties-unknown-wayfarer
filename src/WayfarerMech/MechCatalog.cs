using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace WayfarerMech
{
    public enum MechSlot
    {
        Body,
        ArmLeft,
        ArmRight,
        Legs,
        Booster,
        Horn
    }

    public enum ArmBehavior
    {
        None,
        Drill,
        Cannon,
        BeamDrill,
        Melee
    }

    /// <summary>
    /// One selectable part. IDs are stable save keys; art keys are resolved per palette.
    /// This is the first playable catalogue, not a claim that the whole Starbound
    /// catalogue has been ported.
    /// </summary>
    public sealed class MechPartDef
    {
        public string Id;
        public MechSlot Slot;
        public string Name;
        public string Description;
        public ArmBehavior Behavior;

        // Movement multipliers are combined once when a loadout is equipped.
        public float MoveMul = 1f;
        public float JumpMul = 1f;
        public float ThrustMul = 1f;
        public float RiseMul = 1f;
        public float FallMul = 1f;
        public float MassMul = 1f;

        // Arm tuning; values are multipliers over MechSettings.
        public float RangeMul = 1f;
        public float TerrainMul = 1f;
        public float EntityMul = 1f;
        public float CooldownMul = 1f;
        public bool LongBeam;

        // Body-only hull and rig data, in native art pixels at MechArt.Ppu.
        public float HullW = 4.2f;
        public float HullH = 6.6f;
        public float PilotHeadX = 2f;
        public float PilotHeadY = -30f;
        public float NearArmX = 10f;
        public float NearArmY = 2f;
        public float FarArmX = -2f;
        public float FarArmY = 7f;
        public float HipFrontX = 8f;
        public float HipBackX = -8f;
        public float HipY = 17f;
        public float BoosterX = -20f;
        public float BoosterY = -2f;
        public float HornX = 10f;
        public float HornY = -18f;

        // Legs-only rig data.
        public float ThighLen = 18f;
        public float ShinLen = 17f;
        public float ThighRest = -10f;
        public float ShinRest = 18f;
        public float FootLift = 0f;

        // Arm pivots for the tool tip and muzzle, in the arm sprite's local pixels.
        public float ToolTipX = 30f;
        public float ToolTipY = 0f;
        public float MuzzleX = 30f;
        public float MuzzleY = 0f;

        // Booster flame nozzle offsets relative to the booster sprite pivot.
        public float NozzleAX = -2f;
        public float NozzleAY = 12f;
        public float NozzleBX = 5f;
        public float NozzleBY = 12f;

        public string ArtPrefix;
        // Optional override for catalog families that share one icon.
        public string IconKeyOverride;

        public string IconKey
        {
            get
            {
                if (!string.IsNullOrEmpty(IconKeyOverride)) return IconKeyOverride;
                switch (Slot)
                {
                    case MechSlot.Body: return "icon_body_" + Id;
                    case MechSlot.Legs: return "icon_legs_" + Id;
                    case MechSlot.Booster: return "icon_booster_" + Id;
                    case MechSlot.ArmLeft:
                    case MechSlot.ArmRight: return "icon_" + (Id == "beamdrill" ? "beam" : Id);
                    case MechSlot.Horn: return Id == "siren" ? "icon_siren" : null;
                }
                return null;
            }
        }
    }

    public static class MechCatalog
    {
        public const string DefaultBody = "champion_open";
        public const string DefaultLeftArm = "drill";
        public const string DefaultRightArm = "cannon";
        public const string DefaultLegs = "strider";
        public const string DefaultBooster = "vector";
        public const string DefaultHorn = "none";
        public const string DefaultPalette = "bone";

        public static readonly string[] Palettes = { "bone", "olive", "steel", "violet" };
        public static readonly Dictionary<string, string> PaletteNames = new Dictionary<string, string>
        {
            { "bone", "骨白 / 青绿" },
            { "olive", "橄榄 / 琥珀" },
            { "steel", "冷钢 / 赤红" },
            { "violet", "紫晶 / 青柠" },
        };

        public static readonly Dictionary<string, Color> PalettePrimary = new Dictionary<string, Color>
        {
            { "bone", new Color32(201, 181, 143, 255) },
            { "olive", new Color32(159, 156, 116, 255) },
            { "steel", new Color32(158, 174, 186, 255) },
            { "violet", new Color32(166, 135, 190, 255) },
        };

        private static readonly List<MechPartDef> all = new List<MechPartDef>
        {
            Body("champion_open", "Wayfarer Champion-00", "Wayfarer 原创起点机体；Avian 原型开放舱，低前护板，真实驾驶员头部与表情可见。", 4.6f, 6.9f,
                1.00f, 1.00f, 1.00f, 1.00f, 1.00f, 1.00f,
                -2f, -30f, 14f, 4f, -4f, 8f, 8f, -8f, 16f, -20f, -2f, 12f, -20f),
            Body("venturer_open", "Wayfarer Venturer-00", "Wayfarer 原创工程机体；更宽、更重的开放舱，格栅前脸与双排气。", 4.8f, 6.7f,
                0.88f, 0.95f, 1.18f, 1.08f, 1.22f, 1.00f,
                -1f, -29f, 15f, 3f, -3f, 7f, 9f, -9f, 15f, -21f, -4f, 13f, -18f),

            Legs("strider", "巡行者双腿", "反曲承重腿，速度与跳跃均衡；默认套装。", "leg_strider",
                1.00f, 1.00f, 1.00f, 1.00f, 9f, 9f, -10f, 18f, 0f),
            Legs("hopper", "跳蚤弹跳腿", "短行程弹簧腿；跳跃更高，但水平速度较慢。", "leg_hopper",
                0.82f, 1.28f, 0.98f, 0.94f, 9f, 9f, -18f, 30f, 1.5f),

            Booster("vector", "矢量推进器", "双喷口平衡推进；长按跳跃的持续升力基线。", "booster_vector",
                1.00f, 1.00f, 1.00f, 1.00f, -5f, 4f, 5f, 4f),
            Booster("twinjet", "双联高推推进器", "更大的喷口与更高升力；上升更快，能源视为无限。", "booster_twinjet",
                1.22f, 1.15f, 0.94f, 1.22f, -6f, 4f, 6f, 4f),

            Arm("drill", "机械钻臂", "近距离刷形钻掘，保留原生方块伤害、硬度和掉落；也可伤敌。", ArmBehavior.Drill,
                "arm_drill", 1.00f, 1.00f, 1.00f, 1.00f, 25f, 0f, 25f, 0f),
            Arm("cannon", "实弹机炮", "中距离实弹；对建筑、动物和可攻击角色使用原生伤害路径。", ArmBehavior.Cannon,
                "arm_cannon", 1.00f, 0.85f, 1.10f, 1.00f, 28f, 0f, 28f, 0f),
            Arm("beamdrill", "光束钻", "远距离聚焦钻；射程更远、单点伤害较低，行为与原版机械钻不同。", ArmBehavior.BeamDrill,
                "arm_beam", 1.65f, 0.55f, 0.65f, 1.35f, 30f, 0f, 30f, 0f),

            Horn("none", "空喇叭槽", "没有安装喇叭；合法配置。"),
            Horn("siren", "警告喇叭", "按喇叭键发出可听见的警告声与灯光闪烁；不占用手部。"),
        };

        public static IEnumerable<MechPartDef> All(MechSlot slot) => all.Where(p => p.Slot == slot);
        public static IEnumerable<MechPartDef> Everything => all;

        internal static void Add(MechPartDef part)
        {
            if (part == null || string.IsNullOrEmpty(part.Id)) return;
            if (all.Any(p => p.Slot == part.Slot && p.Id == part.Id)) return;
            all.Add(part);
        }

        // Generated by tools/build_starbound_catalog.py. Runs after the hand-authored
        // starter catalogue so the complete Starbound part list is appended once.
        static MechCatalog()
        {
            StarboundCatalog.Register();
        }

        public static MechPartDef Get(MechSlot slot, string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            return all.FirstOrDefault(p => p.Slot == slot && p.Id == id);
        }

        public static MechPartDef GetArm(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            return all.FirstOrDefault(p => p.Slot == MechSlot.ArmLeft && p.Id == id);
        }

        public static MechPartDef GetOrDefault(MechSlot slot, string id)
        {
            var found = slot == MechSlot.ArmLeft || slot == MechSlot.ArmRight
                ? GetArm(id)
                : Get(slot, id);
            if (found != null) return found;
            switch (slot)
            {
                case MechSlot.Body: return Get(slot, DefaultBody);
                case MechSlot.ArmLeft:
                case MechSlot.ArmRight: return GetArm(DefaultLeftArm);
                case MechSlot.Legs: return Get(slot, DefaultLegs);
                case MechSlot.Booster: return Get(slot, DefaultBooster);
                case MechSlot.Horn: return Get(slot, DefaultHorn);
            }
            return null;
        }

        public static string DefaultFor(MechSlot slot)
        {
            switch (slot)
            {
                case MechSlot.Body: return DefaultBody;
                case MechSlot.ArmLeft: return DefaultLeftArm;
                case MechSlot.ArmRight: return DefaultRightArm;
                case MechSlot.Legs: return DefaultLegs;
                case MechSlot.Booster: return DefaultBooster;
                case MechSlot.Horn: return DefaultHorn;
            }
            return null;
        }

        public static MechStats Combine(MechLoadout loadout)
        {
            var body = GetOrDefault(MechSlot.Body, loadout.BodyId);
            var legs = GetOrDefault(MechSlot.Legs, loadout.LegsId);
            var boost = GetOrDefault(MechSlot.Booster, loadout.BoosterId);
            var left = GetOrDefault(MechSlot.ArmLeft, loadout.LeftArmId);
            var right = GetOrDefault(MechSlot.ArmRight, loadout.RightArmId);
            return new MechStats
            {
                Body = body,
                LeftArm = left,
                RightArm = right,
                Legs = legs,
                Booster = boost,
                HullW = body != null ? body.HullW : 4.2f,
                HullH = body != null ? body.HullH : 6.6f,
                Mass = 60f * (body != null ? body.MassMul : 1f) * (legs != null ? legs.MassMul : 1f),
                MoveMul = (body != null ? body.MoveMul : 1f) * (legs != null ? legs.MoveMul : 1f),
                JumpMul = (body != null ? body.JumpMul : 1f) * (legs != null ? legs.JumpMul : 1f),
                ThrustMul = (body != null ? body.ThrustMul : 1f) * (boost != null ? boost.ThrustMul : 1f),
                RiseMul = (body != null ? body.RiseMul : 1f) * (boost != null ? boost.RiseMul : 1f),
                FallMul = (body != null ? body.FallMul : 1f) * (boost != null ? boost.FallMul : 1f),
            };
        }

        public static List<MechSlot> SlotsInOrder => new List<MechSlot>
        {
            MechSlot.Body, MechSlot.ArmLeft, MechSlot.ArmRight, MechSlot.Legs, MechSlot.Booster, MechSlot.Horn
        };

        private static MechPartDef Body(string id, string name, string desc, float w, float h,
            float move, float jump, float thrust, float rise, float mass, float fall,
            float pilotX, float pilotY, float nearX, float nearY, float farX, float farY,
            float hipFront, float hipBack, float hipY, float boostX, float boostY, float hornX, float hornY)
        {
            return new MechPartDef
            {
                Id = id, Slot = MechSlot.Body, Name = name, Description = desc,
                HullW = w, HullH = h, MoveMul = move, JumpMul = jump, ThrustMul = thrust,
                RiseMul = rise, MassMul = mass, FallMul = fall,
                PilotHeadX = pilotX, PilotHeadY = pilotY,
                NearArmX = nearX, NearArmY = nearY, FarArmX = farX, FarArmY = farY,
                HipFrontX = hipFront, HipBackX = hipBack, HipY = hipY,
                BoosterX = boostX, BoosterY = boostY, HornX = hornX, HornY = hornY,
                ArtPrefix = "body_" + id,
            };
        }

        private static MechPartDef Legs(string id, string name, string desc, string art,
            float move, float jump, float thrust, float mass, float thigh, float shin,
            float thighRest, float shinRest, float footLift)
        {
            return new MechPartDef
            {
                Id = id, Slot = MechSlot.Legs, Name = name, Description = desc,
                MoveMul = move, JumpMul = jump, ThrustMul = thrust, MassMul = mass,
                ThighLen = thigh, ShinLen = shin, ThighRest = thighRest, ShinRest = shinRest,
                FootLift = footLift, ArtPrefix = art,
            };
        }

        private static MechPartDef Booster(string id, string name, string desc, string art,
            float thrust, float rise, float fall, float mass,
            float naX, float naY, float nbX, float nbY)
        {
            return new MechPartDef
            {
                Id = id, Slot = MechSlot.Booster, Name = name, Description = desc,
                ThrustMul = thrust, RiseMul = rise, FallMul = fall, MassMul = mass,
                ArtPrefix = art, NozzleAX = naX, NozzleAY = naY, NozzleBX = nbX, NozzleBY = nbY,
            };
        }

        private static MechPartDef Arm(string id, string name, string desc, ArmBehavior behavior, string art,
            float range, float terrain, float entity, float cooldown, float tipX, float tipY, float muzzleX, float muzzleY)
        {
            return new MechPartDef
            {
                Id = id, Slot = MechSlot.ArmLeft, Name = name, Description = desc, Behavior = behavior,
                ArtPrefix = art, RangeMul = range, TerrainMul = terrain, EntityMul = entity, CooldownMul = cooldown,
                ToolTipX = tipX, ToolTipY = tipY, MuzzleX = muzzleX, MuzzleY = muzzleY,
                LongBeam = behavior == ArmBehavior.BeamDrill,
            };
        }

        private static MechPartDef Horn(string id, string name, string desc)
        {
            return new MechPartDef
            {
                Id = id, Slot = MechSlot.Horn, Name = name, Description = desc,
                ArtPrefix = id == "siren" ? "horn_siren" : null,
                IconKeyOverride = id == "siren" ? "icon_siren" : null,
            };
        }
    }

    public sealed class MechStats
    {
        public MechPartDef Body, LeftArm, RightArm, Legs, Booster;
        public float HullW, HullH, Mass;
        public float MoveMul, JumpMul, ThrustMul, RiseMul, FallMul;
    }
}
