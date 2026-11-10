using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace WayfarerMech
{
    /// <summary>
    /// Side-view modular rig. Only the visual root is flipped; the rigidbody and hull are
    /// never given a negative scale. Body back/seat → real pilot → front cowl → near arm
    /// ordering keeps the face visible and the tools from sweeping across it.
    /// </summary>
    public sealed class MechVisual : MonoBehaviour
    {
        public const float Ppu = MechArt.Ppu;
        private const float LandingDuration = 0.22f;

        private sealed class Leg
        {
            public Transform Thigh, Shin, Foot;
            public MechPartDef Part;
            public bool Front;
        }

        private sealed class ArmRig
        {
            public int Index;
            public MechPartDef Part;
            public Transform Pivot, Tip, Bit, Flash, Beam;
            public SpriteRenderer BitRenderer, FlashRenderer, BeamRenderer;
            public Sprite[] BitFrames, FlashFrames;
            public Vector3 BasePos;
            public float Aim, Recoil, FlashTime, Spin;
            public bool Held, Contact;
        }

        public Transform VisualRoot { get; private set; }
        public Transform PilotAnchor { get; private set; }
        public PilotVisualProxy PilotProxy { get; private set; }
        public int Facing { get; private set; } = 1;
        public MechLoadout Loadout { get; private set; }

        public Transform[] ArmPivots { get; } = new Transform[2];
        public Transform[] ArmTips { get; } = new Transform[2];
        // Compatibility aliases for the observation probe and older call sites.
        public Transform DrillPivot => ArmPivots[0];
        public Transform GunPivot => ArmPivots[1];
        public Transform DrillTip => ArmTips[0];
        public Transform GunMuzzle => ArmTips[1];

        private Transform torsoGroup, bodyBack, bodyFront, booster, horn, shadow;
        private Transform flameA, flameB;
        private SpriteRenderer flameARenderer, flameBRenderer, hornRenderer, shadowRenderer;
        private Sprite[] flameFrames;
        private Leg backLeg, frontLeg;
        private readonly ArmRig[] arms = new ArmRig[2];
        private Vector3 torsoBase;
        private float idleTime, walkPhase, walkBlend, jetBlend, airBlend;
        private float landingTimer, landingImpact, hornTimer;
        private bool thrusting, grounded = true;
        private float velocityX, velocityY;
        private bool built;

        public void Build(Transform root, MechLoadout loadout)
        {
            if (built) return;
            built = true;
            Loadout = loadout ?? MechLoadout.Default();
            var stats = MechCatalog.Combine(Loadout);
            var body = stats.Body;
            var legs = stats.Legs;
            var boost = stats.Booster;
            string palette = Loadout.PaletteId;
            if (Array.IndexOf(MechCatalog.Palettes, palette) < 0) palette = MechCatalog.DefaultPalette;

            VisualRoot = new GameObject("MechVisualRoot").transform;
            VisualRoot.SetParent(root, false);
            VisualRoot.localPosition = Vector3.zero;
            VisualRoot.localRotation = Quaternion.identity;

            torsoGroup = new GameObject("torso-group").transform;
            torsoGroup.SetParent(VisualRoot, false);
            torsoBase = Vector3.zero;

            // Body back and front are separate layers so the pilot can be placed between
            // the seat/engine and the low front cowl.
            bodyBack = new GameObject("body-back").transform;
            bodyBack.SetParent(torsoGroup, false);
            bodyBack.localPosition = Px(0f, -9.6f);
            MakeSprite(MechArt.Get("body_" + body.Id + "_back_" + palette), bodyBack, Vector3.zero, 10);
            bodyFront = new GameObject("body-front").transform;
            bodyFront.SetParent(torsoGroup, false);
            bodyFront.localPosition = Px(0f, -9.6f);
            MakeSprite(MechArt.Get("body_" + body.Id + "_front_" + palette), bodyFront, Vector3.zero, 32);
            PilotAnchor = new GameObject("pilot-anchor").transform;
            PilotAnchor.SetParent(torsoGroup, false);
            PilotAnchor.localPosition = Px(body.PilotHeadX, body.PilotHeadY);
            // A small warm cockpit light keeps the real face readable in the game's very
            // dark caves without brightening the whole level.
            var lightGo = new GameObject("cockpit-light");
            lightGo.transform.SetParent(PilotAnchor, false);
            var cockpitLight = lightGo.AddComponent<Light2D>();
            cockpitLight.lightType = Light2D.LightType.Point;
            cockpitLight.color = new Color(1f, 0.92f, 0.78f, 1f);
            cockpitLight.intensity = 0.62f;
            cockpitLight.pointLightOuterRadius = 5.2f;
            cockpitLight.pointLightInnerRadius = 0.3f;
            cockpitLight.pointLightOuterAngle = 360f;
            cockpitLight.pointLightInnerAngle = 360f;
            cockpitLight.falloffIntensity = 0.5f;

            // Booster behind the body, with two independent flame nozzles.
            booster = new GameObject("booster").transform;
            booster.SetParent(torsoGroup, false);
            booster.localPosition = Px(body.BoosterX, body.BoosterY);
            MakeSprite(MechArt.Get(boost.ArtPrefix + "_" + palette), booster, Vector3.zero, 8);
            flameFrames = new[]
            {
                MechArt.Sprite(boost.ArtPrefix + "_flame0_" + palette),
                MechArt.Sprite(boost.ArtPrefix + "_flame1_" + palette)
            };
            flameA = new GameObject("flame-a").transform;
            flameA.SetParent(booster, false);
            flameA.localPosition = Px(boost.NozzleAX, boost.NozzleAY);
            flameARenderer = MakeSprite(MechArt.Get(boost.ArtPrefix + "_flame0_" + palette), flameA, Vector3.zero, 6);
            flameB = new GameObject("flame-b").transform;
            flameB.SetParent(booster, false);
            flameB.localPosition = Px(boost.NozzleBX, boost.NozzleBY);
            flameBRenderer = MakeSprite(MechArt.Get(boost.ArtPrefix + "_flame1_" + palette), flameB, Vector3.zero, 6);
            if (flameARenderer) flameARenderer.enabled = false;
            if (flameBRenderer) flameBRenderer.enabled = false;

            // Horn is a slot; empty is legal and simply has no sprite. All Starbound
            // horn IDs use the same original siren art/sound family for now.
            if (Loadout.HornId != "none")
            {
                var hornDef = MechCatalog.Get(MechSlot.Horn, Loadout.HornId);
                string hornArt = hornDef != null && !string.IsNullOrEmpty(hornDef.ArtPrefix)
                    ? hornDef.ArtPrefix : "horn_siren";
                horn = new GameObject("horn").transform;
                horn.SetParent(torsoGroup, false);
                horn.localPosition = Px(body.HornX, body.HornY);
                hornRenderer = MakeSprite(MechArt.Get(hornArt + "_" + palette), horn, Vector3.zero, 33);
                if (hornRenderer) hornRenderer.enabled = false;
            }

            backLeg = BuildLeg(false, Px(body.HipBackX, body.HipY), legs, 14);
            frontLeg = BuildLeg(true, Px(body.HipFrontX, body.HipY), legs, 34);
            for (int i = 0; i < 2; i++)
            {
                var part = i == 0 ? stats.LeftArm : stats.RightArm;
                float x = i == 0 ? body.NearArmX : body.FarArmX;
                float y = i == 0 ? body.NearArmY : body.FarArmY;
                arms[i] = BuildArm(i, part, Px(x, y));
            }

            var shadowInfo = MechArt.Get("shadow");
            if (shadowInfo != null)
            {
                shadow = new GameObject("shadow").transform;
                shadow.SetParent(VisualRoot, false);
                shadow.localPosition = Px(0f, 39.5f);
                shadowRenderer = MakeSprite(shadowInfo, shadow, Vector3.zero, 1);
                if (shadowRenderer) shadowRenderer.color = new Color(1f, 1f, 1f, 0.8f);
            }

            // Keep compatibility pointers meaningful even if the player swapped arms.
            ArmPivots[0] = arms[0]?.Pivot;
            ArmPivots[1] = arms[1]?.Pivot;
            ArmTips[0] = arms[0]?.Tip;
            ArmTips[1] = arms[1]?.Tip;
            Plugin.LogInfo("MECH_VISUAL_BUILT body=" + body.Id + " legs=" + legs.Id + " booster=" + boost.Id +
                           " left=" + (stats.LeftArm != null ? stats.LeftArm.Id : "none") +
                           " right=" + (stats.RightArm != null ? stats.RightArm.Id : "none") +
                           " palette=" + palette);
        }

        private Leg BuildLeg(bool front, Vector3 hip, MechPartDef part, int sortingBase)
        {
            var leg = new Leg { Front = front, Part = part };
            var hipT = new GameObject(front ? "hip-front" : "hip-back").transform;
            hipT.SetParent(VisualRoot, false);
            hipT.localPosition = hip;
            var thighInfo = MechArt.Get(part.ArtPrefix + "_thigh_" + Loadout.PaletteId);
            leg.Thigh = new GameObject(front ? "thigh-front" : "thigh-back").transform;
            leg.Thigh.SetParent(hipT, false);
            leg.Thigh.localPosition = Vector3.zero;
            leg.Thigh.localRotation = Quaternion.Euler(0, 0, part.ThighRest);
            var thighSr = MakeSprite(thighInfo, leg.Thigh, Vector3.zero, sortingBase);
            if (thighSr && !front) thighSr.color = new Color(0.68f, 0.72f, 0.78f, 1f);
            var knee = new GameObject(front ? "knee-front" : "knee-back").transform;
            knee.SetParent(leg.Thigh, false);
            knee.localPosition = Px(0f, part.ThighLen);
            var shinInfo = MechArt.Get(part.ArtPrefix + "_shin_" + Loadout.PaletteId);
            leg.Shin = new GameObject(front ? "shin-front" : "shin-back").transform;
            leg.Shin.SetParent(knee, false);
            leg.Shin.localPosition = Vector3.zero;
            leg.Shin.localRotation = Quaternion.Euler(0, 0, part.ShinRest);
            var shinSr = MakeSprite(shinInfo, leg.Shin, Vector3.zero, sortingBase + 1);
            if (shinSr && !front) shinSr.color = new Color(0.68f, 0.72f, 0.78f, 1f);
            var ankle = new GameObject(front ? "ankle-front" : "ankle-back").transform;
            ankle.SetParent(leg.Shin, false);
            ankle.localPosition = Px(0f, part.ShinLen);
            var footInfo = MechArt.Get(part.ArtPrefix + "_foot_" + Loadout.PaletteId);
            leg.Foot = new GameObject(front ? "foot-front" : "foot-back").transform;
            leg.Foot.SetParent(ankle, false);
            leg.Foot.localPosition = Vector3.zero;
            leg.Foot.localRotation = Quaternion.identity;
            var footSr = MakeSprite(footInfo, leg.Foot, Vector3.zero, sortingBase + 2);
            if (footSr && !front) footSr.color = new Color(0.68f, 0.72f, 0.78f, 1f);
            return leg;
        }

        private ArmRig BuildArm(int index, MechPartDef part, Vector3 mount)
        {
            var rig = new ArmRig { Index = index, Part = part };
            if (part == null || part.Behavior == ArmBehavior.None) return rig;
            var palette = Loadout.PaletteId;
            rig.Pivot = new GameObject(index == 0 ? "arm-near" : "arm-far").transform;
            rig.Pivot.SetParent(torsoGroup, false);
            rig.Pivot.localPosition = mount;
            rig.BasePos = mount;
            rig.Pivot.localRotation = Quaternion.identity;
            int sorting = index == 0 ? 38 : 14;
            var armSr = MakeSprite(MechArt.Get(part.ArtPrefix + "_" + palette), rig.Pivot, Vector3.zero, sorting);
            if (armSr && index == 1) armSr.color = new Color(0.68f, 0.72f, 0.78f, 1f);
            rig.Tip = new GameObject("arm-tip").transform;
            rig.Tip.SetParent(rig.Pivot, false);
            rig.Tip.localPosition = Px(part.ToolTipX, part.ToolTipY);

            if (part.Behavior == ArmBehavior.Drill)
            {
                rig.Bit = new GameObject("drill-bit").transform;
                rig.Bit.SetParent(rig.Pivot, false);
                rig.Bit.localPosition = Px(part.ToolTipX - 13f, part.ToolTipY);
                rig.BitFrames = new[]
                {
                    MechArt.Sprite("arm_drill_bit0_" + palette),
                    MechArt.Sprite("arm_drill_bit1_" + palette)
                };
                rig.BitRenderer = MakeSprite(MechArt.Get("arm_drill_bit0_" + palette), rig.Bit, Vector3.zero, sorting + 1);
            }
            else if (part.Behavior == ArmBehavior.Cannon)
            {
                rig.Flash = new GameObject("muzzle-flash").transform;
                rig.Flash.SetParent(rig.Pivot, false);
                rig.Flash.localPosition = Px(part.MuzzleX, part.MuzzleY);
                rig.FlashFrames = new[]
                {
                    MechArt.Sprite("arm_cannon_flash0_" + palette),
                    MechArt.Sprite("arm_cannon_flash1_" + palette)
                };
                rig.FlashRenderer = MakeSprite(MechArt.Get("arm_cannon_flash0_" + palette), rig.Flash, Vector3.zero, 50);
                if (rig.FlashRenderer) rig.FlashRenderer.enabled = false;
            }
            else if (part.Behavior == ArmBehavior.BeamDrill)
            {
                rig.Beam = new GameObject("beam").transform;
                rig.Beam.SetParent(rig.Pivot, false);
                rig.Beam.localPosition = Px(part.ToolTipX, part.MuzzleY);
                rig.BeamRenderer = MakeSprite(MechArt.Get("arm_beam_glow_" + palette), rig.Beam, Vector3.zero, 48);
                if (rig.BeamRenderer) rig.BeamRenderer.enabled = false;
            }
            return rig;
        }

        private static Material mechMaterial;

        private static Material MechMaterial()
        {
            if (mechMaterial) return mechMaterial;
            var shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
            if (shader)
            {
                mechMaterial = new Material(shader) { name = "WayfarerMechUnlit" };
            }
            else
            {
                Plugin.LogError("Unlit 2D sprite shader not found; mech uses the lit default material.");
            }
            return mechMaterial;
        }

        private static SpriteRenderer MakeSprite(MechArt.SpriteInfo info, Transform parent, Vector3 local, int sorting)
        {
            if (info == null || info.Sprite == null)
            {
                Plugin.LogError("Missing visual sprite; part omitted.");
                return null;
            }
            var go = new GameObject(info.Key);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = local;
            go.transform.localRotation = Quaternion.identity;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = info.Sprite;
            sr.sortingOrder = sorting;
            var mat = MechMaterial();
            if (mat) sr.sharedMaterial = mat;
            return sr;
        }

        private static Vector3 Px(float x, float y) => new Vector3(x / Ppu, -y / Ppu, 0f);

        public void SetFacing(int sign)
        {
            if (sign == 0) return;
            Facing = sign > 0 ? 1 : -1;
            if (VisualRoot) VisualRoot.localScale = new Vector3(Facing, 1f, 1f);
        }

        /// <summary>Angles are local-space degrees; 0 points forward, positive is up.</summary>
        public void SetAim(float nearLocalDeg, float farLocalDeg)
        {
            SetArmAim(0, nearLocalDeg);
            SetArmAim(1, farLocalDeg);
        }

        public void SetArmAim(int index, float localDeg)
        {
            if (index < 0 || index >= 2 || arms[index] == null) return;
            arms[index].Aim = Mathf.Clamp(localDeg, -58f, 58f);
            if (arms[index].Pivot) arms[index].Pivot.localRotation = Quaternion.Euler(0, 0, arms[index].Aim);
        }

        public void SetMotion(float vx, float vy, bool isGrounded, bool thrust)
        {
            velocityX = vx; velocityY = vy; grounded = isGrounded; thrusting = thrust;
        }

        public void SetArmActivity(int index, bool held, bool contact)
        {
            if (index < 0 || index >= 2 || arms[index] == null) return;
            arms[index].Held = held;
            arms[index].Contact = contact;
        }

        public void PulseFire(int index)
        {
            if (index < 0 || index >= 2 || arms[index] == null) return;
            var a = arms[index];
            a.Recoil = 1f;
            a.FlashTime = 0.055f;
            if (a.FlashRenderer)
            {
                a.FlashRenderer.enabled = true;
                if (a.FlashFrames != null && a.FlashFrames.Length > 0)
                    a.FlashRenderer.sprite = a.FlashFrames[UnityEngine.Random.Range(0, a.FlashFrames.Length)];
            }
        }

        public void SetBeam(int index, bool active, float lengthPx)
        {
            if (index < 0 || index >= 2 || arms[index] == null) return;
            var a = arms[index];
            if (a.BeamRenderer == null) return;
            a.BeamRenderer.enabled = active;
            if (!active) return;
            float baseWidth = a.BeamRenderer.sprite != null ? a.BeamRenderer.sprite.bounds.size.x : 6f;
            float sx = Mathf.Max(0.01f, lengthPx / Ppu / Mathf.Max(0.01f, baseWidth));
            a.Beam.localScale = new Vector3(sx, 1f, 1f);
        }

        public void NotifyLanded(float impact)
        {
            landingTimer = LandingDuration;
            landingImpact = Mathf.Clamp01(impact);
        }

        public void Honk()
        {
            hornTimer = 0.9f;
        }

        public void AttachPilot(Body pilot)
        {
            DetachPilot();
            if (!PilotAnchor || !pilot) return;
            PilotProxy = PilotVisualProxy.Create(pilot, PilotAnchor, Vector3.zero, 24);
        }

        public void DetachPilot()
        {
            if (PilotProxy)
            {
                PilotProxy.Teardown();
                Destroy(PilotProxy.gameObject);
                PilotProxy = null;
            }
        }

        public void DestroyVisual()
        {
            DetachPilot();
            if (VisualRoot)
            {
                Destroy(VisualRoot.gameObject);
                VisualRoot = null;
            }
            built = false;
        }

        private void OnDestroy()
        {
            DetachPilot();
            if (VisualRoot) Destroy(VisualRoot.gameObject);
            built = false;
        }

        public void SetHornVisible(bool visible)
        {
            if (hornRenderer) hornRenderer.enabled = visible;
        }

        private void Update()
        {
            if (!built) return;
            float dt = Time.deltaTime;
            if (dt <= 0f) return;
            idleTime += dt;
            bool moving = grounded && Mathf.Abs(velocityX) > 0.2f;
            float targetWalk = moving ? Mathf.Clamp01(Mathf.Abs(velocityX) / 6f) : 0f;
            walkBlend = Mathf.MoveTowards(walkBlend, targetWalk, dt * 7f);
            if (moving) walkPhase = Mathf.Repeat(walkPhase + dt * (0.9f + Mathf.Abs(velocityX) * 0.16f), 1f);
            jetBlend = Mathf.MoveTowards(jetBlend, thrusting ? 1f : 0f, dt * 7f);
            airBlend = Mathf.MoveTowards(airBlend, grounded ? 0f : 1f, dt * 5f);
            if (landingTimer > 0f) landingTimer = Mathf.Max(0f, landingTimer - dt);
            if (hornTimer > 0f) hornTimer = Mathf.Max(0f, hornTimer - dt);

            float gaitBob = Mathf.Abs(Mathf.Sin(walkPhase * Mathf.PI * 2f)) * 0.025f * walkBlend;
            float idleBob = Mathf.Sin(idleTime * 1.6f) * 0.006f * (1f - walkBlend);
            float land = landingTimer > 0f ? Mathf.Sin(landingTimer / LandingDuration * Mathf.PI) * 0.085f * (0.4f + landingImpact) : 0f;
            float pitch = -2.5f * jetBlend + Mathf.Sin(walkPhase * Mathf.PI * 2f) * 0.7f * walkBlend;
            torsoGroup.localPosition = torsoBase + new Vector3(0f, gaitBob + idleBob - land, 0f);
            torsoGroup.localRotation = Quaternion.Euler(0, 0, pitch);

            AnimateLeg(backLeg, 0.5f);
            AnimateLeg(frontLeg, 0f);
            AnimateArms(dt);
            AnimateFlames(dt);
            if (shadowRenderer) shadowRenderer.enabled = grounded;
            if (hornRenderer)
            {
                bool blink = hornTimer > 0f && Mathf.Repeat(idleTime * 12f, 1f) < 0.5f;
                hornRenderer.enabled = blink;
            }
        }

        private void AnimateLeg(Leg leg, float phaseOffset)
        {
            if (leg?.Thigh == null || leg.Shin == null || leg.Foot == null || leg.Part == null) return;
            float A = 10f, K = 20f;
            float p = Mathf.Repeat(walkPhase + phaseOffset, 1f);
            float thighGait, shinGait;
            if (p < 0.5f)
            {
                float t = Smooth(p / 0.5f);
                thighGait = Mathf.Lerp(A, -A, t);
                shinGait = 0f;
            }
            else
            {
                float t = Smooth((p - 0.5f) / 0.5f);
                thighGait = Mathf.Lerp(-A, A, t);
                shinGait = Mathf.Sin(t * Mathf.PI) * K;
            }
            float thigh = Mathf.Lerp(leg.Part.ThighRest, leg.Part.ThighRest + thighGait, walkBlend);
            float shin = Mathf.Lerp(leg.Part.ShinRest, leg.Part.ShinRest + shinGait, walkBlend);
            float tuck = airBlend * (0.35f + 0.65f * jetBlend);
            thigh += tuck * (leg.Front ? 10f : -8f);
            shin += tuck * 24f;
            float landingBend = landingTimer > 0f ? Mathf.Sin(landingTimer / LandingDuration * Mathf.PI) * 12f * (0.3f + landingImpact) : 0f;
            shin += landingBend;
            thigh -= landingBend * 0.35f;
            leg.Thigh.localRotation = Quaternion.Euler(0, 0, thigh);
            leg.Shin.localRotation = Quaternion.Euler(0, 0, shin);
            float toe = Mathf.Sin(p * Mathf.PI * 2f) * 3f * walkBlend;
            leg.Foot.localRotation = Quaternion.Euler(0, 0, -(thigh + shin) + toe);
        }

        private void AnimateArms(float dt)
        {
            for (int i = 0; i < 2; i++)
            {
                var a = arms[i];
                if (a == null || a.Pivot == null) continue;
                a.Spin += dt;
                float jitter = 0f;
                if (a.Held && a.Part != null && a.Part.Behavior == ArmBehavior.Drill)
                {
                    float speed = a.Contact ? 1f : 0.35f;
                    jitter = Mathf.Sin(a.Spin * (a.Contact ? 90f : 30f)) * (a.Contact ? 0.7f : 0.25f);
                    a.Pivot.localPosition = a.BasePos + new Vector3(0f, Mathf.Sin(a.Spin * 70f) * 0.01f * speed, 0f);
                }
                else if (a.Pivot)
                {
                    a.Pivot.localPosition = a.BasePos;
                }
                if (a.Pivot) a.Pivot.localRotation = Quaternion.Euler(0, 0, a.Aim + jitter);
                if (a.BitRenderer)
                {
                    if (a.Held)
                    {
                        int frame = ((int)(a.Spin * (a.Contact ? 30f : 12f))) & 1;
                        if (a.BitFrames != null && a.BitFrames[frame]) a.BitRenderer.sprite = a.BitFrames[frame];
                    }
                    else if (a.BitFrames != null && a.BitFrames[0]) a.BitRenderer.sprite = a.BitFrames[0];
                }
                if (a.Recoil > 0f)
                {
                    a.Recoil = Mathf.MoveTowards(a.Recoil, 0f, dt * 9f);
                    a.Pivot.localPosition = a.BasePos - new Vector3(a.Recoil * 0.06f, 0f, 0f);
                    a.Pivot.localRotation = Quaternion.Euler(0, 0, a.Aim + a.Recoil * 3f);
                }
                if (a.FlashTime > 0f)
                {
                    a.FlashTime -= dt;
                    if (a.FlashTime <= 0f && a.FlashRenderer) a.FlashRenderer.enabled = false;
                }
            }
        }

        private void AnimateFlames(float dt)
        {
            bool active = thrusting && jetBlend > 0.08f;
            if (flameARenderer) flameARenderer.enabled = active;
            if (flameBRenderer) flameBRenderer.enabled = active;
            if (!active) return;
            int frame = ((int)(idleTime * 14f)) & 1;
            if (flameFrames != null)
            {
                if (flameARenderer && flameFrames[frame]) flameARenderer.sprite = flameFrames[frame];
                if (flameBRenderer && flameFrames[frame ^ 1]) flameBRenderer.sprite = flameFrames[frame ^ 1];
            }
            float flicker = 0.85f + 0.15f * Mathf.Sin(idleTime * 32f);
            if (flameA) flameA.localScale = new Vector3(0.9f + 0.08f * Mathf.Sin(idleTime * 41f), flicker * jetBlend, 1f);
            if (flameB) flameB.localScale = new Vector3(0.85f + 0.1f * Mathf.Sin(idleTime * 37f + 1.7f), (0.8f + 0.2f * Mathf.Cos(idleTime * 29f)) * jetBlend, 1f);
            var c = new Color(1f, 1f, 1f, 0.85f);
            if (flameARenderer) flameARenderer.color = c;
            if (flameBRenderer) flameBRenderer.color = c;
        }

        private static float Smooth(float t)
        {
            t = Mathf.Clamp01(t);
            return t * t * (3f - 2f * t);
        }
    }
}
