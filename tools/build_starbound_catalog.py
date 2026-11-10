#!/usr/bin/env python3
"""Generate the complete Wayfarer part catalogue from the local Starbound item list.

Only IDs, names and coarse stat/tier facts are read. No Starbound pixels or item
text are copied into the generated code: descriptions are original adaptation text.

Outputs:
  tools/art/starbound_catalog.json      source data for the art generator and audit
  src/WayfarerMech/StarboundCatalog.g.cs  runtime catalogue appended by MechCatalog

Run from D:\\testGround\\furry:
  python tools/build_starbound_catalog.py
"""
from __future__ import annotations

import json
import os
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
RAW = Path(r"E:\GameArtLibrary\starbound-mechs\source\raw\items\generic\mechparts")
OUT_JSON = ROOT / "tools" / "art" / "starbound_catalog.json"
OUT_CS = ROOT / "src" / "WayfarerMech" / "StarboundCatalog.g.cs"

# Body style keys are original reinterpretation choices. Colors are approximate
# palette directions, not extracted pixel data. The beak/crest stay ivory in art.
BODY_STYLES = {
    "apex": ("Dominion", "red", "horns", 5.0, 7.1, 1.10, 0.92, 1.08, 1.20),
    "apexproto": ("Dominion-00", "red", "horns", 4.7, 6.9, 1.02, 0.97, 1.03, 1.05),
    "avian": ("Champion", "gold", "wing", 4.8, 7.1, 1.00, 1.04, 1.04, 1.06),
    "avianproto": ("Champion-00", "gold", "wing", 4.6, 6.9, 1.00, 1.00, 1.00, 1.00),
    "bad": ("Beat", "crimson", "blocky", 4.8, 7.0, 1.06, 0.94, 1.06, 1.12),
    "cultist": ("Occasus", "violet", "fin", 4.8, 7.1, 1.02, 1.02, 1.04, 1.08),
    "cultist2": ("Occasus-2", "violet", "crown", 5.0, 7.2, 1.05, 1.05, 1.08, 1.12),
    "exodus": ("Exodus", "black-gold", "reactor", 5.0, 7.2, 1.08, 1.00, 1.10, 1.16),
    "floran": ("Golem", "green", "leaf", 4.8, 7.0, 1.00, 1.08, 1.04, 1.06),
    "floranproto": ("Golem-00", "green", "leaf", 4.6, 6.9, 1.00, 1.04, 1.02, 1.00),
    "glitch": ("Arthuria", "grey-gold", "blocky", 4.8, 7.0, 0.98, 0.98, 1.06, 1.12),
    "glitchproto": ("Arthuria-00", "grey-gold", "blocky", 4.6, 6.9, 0.98, 1.00, 1.03, 1.04),
    "hero": ("Hero", "blue-gold", "wing", 4.8, 7.1, 1.04, 1.04, 1.06, 1.08),
    "human": ("Venturer", "blue-red", "visor", 4.8, 7.0, 1.00, 1.00, 1.02, 1.04),
    "humanproto": ("Venturer-00", "blue-red", "visor", 4.6, 6.9, 1.00, 1.00, 1.00, 1.00),
    "hylotl": ("Kabuto", "teal-white", "fin", 4.8, 7.0, 1.02, 1.06, 1.04, 1.06),
    "hylotlproto": ("Kabuto-00", "teal-white", "fin", 4.6, 6.9, 1.00, 1.04, 1.02, 1.00),
    "iris": ("Cyclops", "lavender-teal", "visor", 5.0, 7.1, 1.04, 1.00, 1.08, 1.12),
    "military": ("USCM", "olive-red", "blocky", 5.0, 7.2, 1.02, 0.96, 1.10, 1.20),
    "miniknog": ("Miniknog", "grey-green", "visor", 5.0, 7.2, 1.04, 1.00, 1.08, 1.16),
    "novakid": ("Marshal", "orange-blue", "crown", 4.8, 7.0, 1.06, 1.04, 1.02, 1.04),
    "novakidproto": ("Marshal-00", "orange-blue", "crown", 4.6, 6.9, 1.03, 1.02, 1.00, 1.00),
    "peacekeeper": ("Peacekeeper", "white-blue", "wing", 5.0, 7.1, 1.02, 1.02, 1.08, 1.12),
    "penguin": ("Shockhopper", "black-white", "fin", 4.8, 6.9, 0.98, 1.02, 1.04, 1.04),
    "pirate": ("Skulduggery", "red-dark", "horns", 5.0, 7.1, 1.06, 0.96, 1.08, 1.12),
    "protector": ("Protector", "white-gold", "wing", 5.0, 7.2, 1.04, 1.04, 1.10, 1.16),
    "reactor": ("Reactor", "green-glow", "reactor", 5.0, 7.2, 1.02, 1.00, 1.12, 1.22),
    "shark": ("Lilotron", "blue-white", "fin", 5.0, 7.1, 1.08, 1.02, 1.04, 1.14),
    "skull": ("Nightwinder", "bone-dark", "horns", 5.0, 7.1, 1.04, 1.00, 1.06, 1.10),
}

ARM_STYLES = {
    "beamdrill": ("beam", "BeamDrill", 1.65, 0.55, 0.65, 1.35, 30, 30),
    "beamsniper": ("beam", "BeamDrill", 2.10, 0.35, 1.15, 1.60, 32, 32),
    "cannon": ("gun", "Cannon", 1.00, 0.85, 1.10, 1.00, 28, 28),
    "chainsaw": ("drill", "Melee", 0.90, 1.20, 1.30, 0.85, 24, 24),
    "clustermine": ("gun", "Cannon", 1.30, 0.50, 0.80, 1.80, 28, 28),
    "despawner": ("gun", "None", 1.0, 0.0, 0.0, 1.0, 28, 28),
    "drill": ("drill", "Drill", 1.00, 1.00, 1.00, 1.00, 25, 25),
    "dualrifle": ("gun", "Cannon", 1.25, 0.70, 1.05, 0.90, 30, 30),
    "energyblade": ("drill", "Melee", 0.85, 0.80, 1.55, 0.80, 24, 24),
    "energyrifle": ("gun", "Cannon", 1.45, 0.65, 1.10, 1.00, 30, 30),
    "flakcannon": ("gun", "Cannon", 1.20, 0.60, 1.25, 1.30, 30, 30),
    "gatling": ("gun", "Cannon", 1.15, 0.65, 1.15, 0.65, 30, 30),
    "gravitymine": ("gun", "Cannon", 1.30, 0.50, 0.80, 1.80, 28, 28),
    "guidedmissiles": ("gun", "Cannon", 2.00, 0.55, 1.70, 1.80, 32, 32),
    "heatrifle": ("gun", "Cannon", 1.50, 0.80, 1.25, 1.30, 32, 32),
    "homingmissile": ("gun", "Cannon", 2.10, 0.45, 1.50, 2.00, 32, 32),
    "missilerack": ("gun", "Cannon", 1.90, 0.50, 1.60, 2.10, 30, 30),
    "multidronelauncher": ("gun", "Cannon", 1.60, 0.40, 0.90, 1.70, 28, 28),
    "neolaser": ("beam", "BeamDrill", 1.85, 0.50, 1.00, 1.20, 32, 32),
    "plasmadronelauncher": ("gun", "Cannon", 1.60, 0.40, 1.00, 1.70, 28, 28),
    "shielddronelauncher": ("gun", "Cannon", 1.55, 0.40, 0.70, 1.80, 28, 28),
    "soluskatana": ("drill", "Melee", 0.95, 0.95, 1.80, 0.85, 26, 26),
    "spikefist": ("drill", "Melee", 0.85, 1.10, 1.40, 0.80, 24, 24),
    "splitshot": ("gun", "Cannon", 1.25, 0.55, 1.00, 1.15, 28, 28),
    "teslastream": ("beam", "BeamDrill", 1.90, 0.45, 1.10, 1.40, 32, 32),
    "tommygun": ("gun", "Cannon", 1.35, 0.60, 0.95, 0.65, 30, 30),
    "wobbleshot": ("gun", "Cannon", 1.40, 0.55, 1.05, 1.10, 28, 28),
}

LEG_STYLES = {
    "arc": ("arc", 1.28, 0.92, 9, 9, -10, 18, 0),
    "hermes": ("strider", 1.22, 1.02, 9, 9, -10, 18, 0),
    "hopper": ("hopper", 0.82, 1.28, 9, 9, -18, 30, 1.5),
    "intrepid": ("sleek", 1.12, 1.12, 9, 9, -12, 22, 0.6),
    "ronin": ("arc", 1.08, 1.04, 9, 9, -10, 20, 0.2),
    "simple": ("strider", 1.00, 1.00, 9, 9, -10, 18, 0),
    "sleek": ("sleek", 1.16, 1.00, 9, 9, -12, 22, 0.4),
    "twinjet": ("sleek", 1.18, 1.08, 9, 9, -12, 22, 0.4),
}

BOOSTER_STYLES = {
    "astro": ("vector", 1.05, 1.08, 0.96, 1.02),
    "gallant": ("vector", 1.16, 1.12, 0.94, 1.08),
    "hammer": ("twinjet", 1.24, 1.00, 1.16, 1.12),
    "simple": ("vector", 1.00, 1.00, 1.00, 1.00),
    "sleek": ("twinjet", 1.10, 1.06, 1.02, 1.04),
    "stalwart": ("vector", 1.08, 1.10, 0.92, 1.06),
    "zed": ("twinjet", 1.28, 1.18, 0.94, 1.14),
    "zero": ("vector", 1.20, 1.20, 0.88, 1.10),
}

HORN_IDS = [
    "airhorn", "bikebell", "bull", "charge", "clownhorn", "foghorn",
    "lacucha", "robotvoice", "rubberduck", "sportshorn", "steamwhistle",
    "toy", "train", "truck",
]

SLOT_ART = {
    "drill": ("arm_drill", "icon_drill"),
    "gun": ("arm_cannon", "icon_cannon"),
    "beam": ("arm_beam", "icon_beam"),
}

THEME_RGB = {
    "red": ((42, 18, 17), (72, 28, 25), (112, 42, 35), (160, 62, 48), (210, 94, 70)),
    "gold": ((44, 38, 22), (74, 64, 35), (112, 94, 50), (156, 130, 72), (210, 182, 112)),
    "crimson": ((33, 12, 18), (60, 18, 27), (96, 28, 38), (142, 44, 54), (200, 72, 78)),
    "violet": ((32, 20, 45), (54, 32, 72), (84, 48, 110), (120, 72, 154), (170, 112, 210)),
    "black-gold": ((18, 18, 20), (32, 30, 28), (56, 50, 42), (96, 82, 54), (160, 138, 84)),
    "green": ((24, 38, 24), (40, 62, 34), (64, 94, 46), (92, 128, 60), (132, 172, 82)),
    "grey-gold": ((34, 36, 38), (58, 60, 60), (88, 88, 82), (126, 120, 98), (176, 164, 126)),
    "blue-gold": ((22, 32, 58), (34, 50, 88), (52, 76, 128), (78, 108, 168), (118, 150, 206)),
    "blue-red": ((24, 32, 52), (38, 54, 82), (62, 82, 120), (104, 60, 56), (164, 84, 72)),
    "teal-white": ((18, 42, 46), (30, 66, 70), (46, 98, 102), (78, 136, 138), (126, 180, 176)),
    "lavender-teal": ((46, 32, 62), (74, 52, 98), (108, 78, 138), (60, 108, 108), (112, 168, 160)),
    "olive-red": ((32, 34, 20), (54, 56, 32), (86, 88, 48), (124, 56, 42), (174, 82, 54)),
    "grey-green": ((30, 38, 30), (48, 62, 46), (72, 92, 68), (102, 128, 88), (148, 172, 124)),
    "orange-blue": ((54, 34, 20), (90, 54, 28), (136, 78, 34), (60, 92, 136), (112, 148, 196)),
    "white-blue": ((56, 58, 62), (88, 92, 96), (128, 134, 140), (76, 106, 154), (134, 168, 214)),
    "black-white": ((20, 20, 22), (36, 36, 38), (60, 60, 62), (110, 112, 116), (180, 184, 188)),
    "red-dark": ((30, 14, 16), (54, 20, 22), (86, 28, 30), (126, 42, 42), (176, 66, 62)),
    "white-gold": ((58, 56, 48), (90, 86, 70), (130, 122, 98), (174, 158, 112), (222, 204, 154)),
    "green-glow": ((20, 40, 30), (32, 66, 46), (48, 98, 64), (72, 138, 86), (118, 196, 112)),
    "blue-white": ((22, 34, 54), (36, 56, 84), (56, 86, 122), (102, 138, 170), (174, 200, 216)),
    "bone-dark": ((38, 34, 28), (62, 56, 44), (96, 86, 66), (138, 124, 92), (190, 176, 138)),
}


def item_name(slot: str, sid: str, fallback: str) -> str:
    p = RAW / slot / f"mech{slot[:-1] if slot != 'legs' else 'legs'}{sid}.item"
    # file naming is mechbodyX / mecharmX / mechlegsX / mechboosterX / mechhornX
    prefix = {"body": "mechbody", "arm": "mecharm", "legs": "mechlegs",
              "booster": "mechbooster", "horn": "mechhorn"}[slot]
    p = RAW / slot / f"{prefix}{sid}.item"
    try:
        d = json.loads(p.read_text(encoding="utf-8-sig"))
        s = d.get("shortdescription") or fallback
        for suffix in (" Mech Body", " Mech Arm", " Mech Legs", " Mech Boosters", " Mech Horn"):
            if s.endswith(suffix):
                s = s[: -len(suffix)]
        return s
    except Exception:
        return fallback


def parse_ids(slot: str):
    prefix = {"body": "mechbody", "arm": "mecharm", "legs": "mechlegs",
              "booster": "mechbooster", "horn": "mechhorn"}[slot]
    out = []
    for p in sorted((RAW / slot).glob("*.item")):
        sid = p.stem[len(prefix):]
        out.append(sid)
    return out


def csharp_string(s: str) -> str:
    return json.dumps(s, ensure_ascii=False)


def f(v) -> str:
    return f"{float(v):.2f}f"


def theme(name: str):
    return THEME_RGB.get(name, THEME_RGB["grey-gold"])


def main():
    catalog = {"source": "local Starbound item list; IDs/names only; original art adaptations",
               "bodies": [], "arms": [], "legs": [], "boosters": [], "horns": []}
    cs = []
    cs.append("// <auto-generated by tools/build_starbound_catalog.py>")
    cs.append("// IDs and short names are catalogue facts; descriptions and art adaptations are original.")
    cs.append("namespace WayfarerMech")
    cs.append("{")
    cs.append("    internal static class StarboundCatalog")
    cs.append("    {")
    cs.append("        internal static void Register()")
    cs.append("        {")

    for sid in parse_ids("body"):
        if sid not in BODY_STYLES:
            continue
        disp, color, deco, hw, hh, move, jump, thrust, mass = BODY_STYLES[sid]
        name = item_name("body", sid, disp)
        rgb = theme(color)[2]
        accent = tuple(min(255, 255 - c // 2 + 20) for c in rgb)
        desc = f"Starbound「{name}」机体的原创像素重绘；开放舱、模块化适配。配色调性: {color}。"
        catalog["bodies"].append({
            "id": "sb_" + sid, "starbound_id": sid, "name": name,
            "color": color, "deco": deco, "hull": [hw, hh],
            "move": move, "jump": jump, "thrust": thrust, "mass": mass,
            "theme_secondary": list(rgb), "theme_accent": list(accent),
            "art": "body_sb_" + sid,
        })
        cs.append("            MechCatalog.Add(new MechPartDef {")
        cs.append(f"                Id = {csharp_string('sb_' + sid)}, Slot = MechSlot.Body, Name = {csharp_string(name)},")
        cs.append(f"                Description = {csharp_string(desc)},")
        cs.append(f"                HullW = {f(hw)}, HullH = {f(hh)}, MoveMul = {f(move)}, JumpMul = {f(jump)},")
        cs.append(f"                ThrustMul = {f(thrust)}, MassMul = {f(mass)},")
        cs.append("                PilotHeadX = -2f, PilotHeadY = -30f, NearArmX = 14f, NearArmY = 4f,")
        cs.append("                FarArmX = -4f, FarArmY = 8f, HipFrontX = 8f, HipBackX = -8f, HipY = 16f,")
        cs.append("                BoosterX = -20f, BoosterY = -2f, HornX = 10f, HornY = -18f,")
        cs.append(f"                ArtPrefix = {csharp_string('body_sb_' + sid)}, IconKeyOverride = {csharp_string('icon_body_sb_' + sid)}")
        cs.append("            });")

    for sid, (family, behavior, rng, terrain, entity, cooldown, tip, muzzle) in ARM_STYLES.items():
        name = item_name("arm", sid, sid)
        art, icon = SLOT_ART[family]
        desc = f"Starbound「{name}」臂的适配：{behavior} 行为族，原创图标/视觉；不是对原版弹道的逐项复刻。"
        catalog["arms"].append({
            "id": "sb_" + sid, "starbound_id": sid, "name": name,
            "behavior": behavior, "family": family, "art": art, "icon": icon,
            "range": rng, "terrain": terrain, "entity": entity, "cooldown": cooldown,
        })
        cs.append("            MechCatalog.Add(new MechPartDef {")
        cs.append(f"                Id = {csharp_string('sb_' + sid)}, Slot = MechSlot.ArmLeft, Name = {csharp_string(name)},")
        cs.append(f"                Description = {csharp_string(desc)}, Behavior = ArmBehavior.{behavior},")
        cs.append(f"                ArtPrefix = {csharp_string(art)}, IconKeyOverride = {csharp_string(icon)},")
        cs.append(f"                RangeMul = {f(rng)}, TerrainMul = {f(terrain)}, EntityMul = {f(entity)}, CooldownMul = {f(cooldown)},")
        cs.append(f"                ToolTipX = {f(tip)}, ToolTipY = 0f, MuzzleX = {f(muzzle)}, MuzzleY = 0f,")
        cs.append(f"                LongBeam = {'true' if behavior == 'BeamDrill' else 'false'}")
        cs.append("            });")

    for sid, (family, move, jump, thigh, shin, trest, srest, lift) in LEG_STYLES.items():
        name = item_name("legs", sid, sid)
        art = "leg_" + family
        desc = f"Starbound「{name}」腿的适配；视觉使用原创腿型族 {family}，属性独立。"
        catalog["legs"].append({"id": "sb_" + sid, "starbound_id": sid, "name": name,
                                "family": family, "art": art, "move": move, "jump": jump})
        cs.append("            MechCatalog.Add(new MechPartDef {")
        cs.append(f"                Id = {csharp_string('sb_' + sid)}, Slot = MechSlot.Legs, Name = {csharp_string(name)},")
        cs.append(f"                Description = {csharp_string(desc)}, ArtPrefix = {csharp_string(art)}, IconKeyOverride = {csharp_string('icon_legs_' + family)},")
        cs.append(f"                MoveMul = {f(move)}, JumpMul = {f(jump)}, ThighLen = {f(thigh)}, ShinLen = {f(shin)},")
        cs.append(f"                ThighRest = {f(trest)}, ShinRest = {f(srest)}, FootLift = {f(lift)}")
        cs.append("            });")

    for sid, (family, thrust, rise, fall, mass) in BOOSTER_STYLES.items():
        name = item_name("booster", sid, sid)
        art = "booster_" + family
        desc = f"Starbound「{name}」推进器的适配；视觉使用原创推进器族 {family}，属性独立。"
        catalog["boosters"].append({"id": "sb_" + sid, "starbound_id": sid, "name": name,
                                    "family": family, "art": art, "thrust": thrust, "rise": rise, "fall": fall})
        cs.append("            MechCatalog.Add(new MechPartDef {")
        cs.append(f"                Id = {csharp_string('sb_' + sid)}, Slot = MechSlot.Booster, Name = {csharp_string(name)},")
        cs.append(f"                Description = {csharp_string(desc)}, ArtPrefix = {csharp_string(art)}, IconKeyOverride = {csharp_string('icon_booster_' + family)},")
        cs.append(f"                ThrustMul = {f(thrust)}, RiseMul = {f(rise)}, FallMul = {f(fall)}, MassMul = {f(mass)},")
        cs.append("                NozzleAX = -5f, NozzleAY = 4f, NozzleBX = 5f, NozzleBY = 4f")
        cs.append("            });")

    for sid in HORN_IDS:
        name = item_name("horn", sid, sid)
        desc = f"Starbound「{name}」喇叭的适配；使用现有警告声与灯光表现，不复制原版音频。"
        catalog["horns"].append({"id": "sb_" + sid, "starbound_id": sid, "name": name,
                                 "art": "horn_siren", "icon": "icon_siren"})
        cs.append("            MechCatalog.Add(new MechPartDef {")
        cs.append(f"                Id = {csharp_string('sb_' + sid)}, Slot = MechSlot.Horn, Name = {csharp_string(name)},")
        cs.append(f"                Description = {csharp_string(desc)}, ArtPrefix = \"horn_siren\", IconKeyOverride = \"icon_siren\"")
        cs.append("            });")

    cs.append("        }")
    cs.append("    }")
    cs.append("}")
    OUT_JSON.parent.mkdir(parents=True, exist_ok=True)
    OUT_JSON.write_text(json.dumps(catalog, indent=2, ensure_ascii=False), encoding="utf-8")
    OUT_CS.write_text("\n".join(cs) + "\n", encoding="utf-8")
    print(f"bodies={len(catalog['bodies'])} arms={len(catalog['arms'])} legs={len(catalog['legs'])} "
          f"boosters={len(catalog['boosters'])} horns={len(catalog['horns'])}")
    print(OUT_JSON)
    print(OUT_CS)


if __name__ == "__main__":
    main()
