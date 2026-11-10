using System;
using System.Collections.Generic;
using UnityEngine;
using WayfarerMech.Core;

namespace WayfarerMech
{
    public sealed class MechController : MonoBehaviour
    {
        public static readonly Vector2 Size = new Vector2(4.2f, 6.6f);
        private const int MaxDrillCells = 64;
        private static readonly System.Diagnostics.Stopwatch DrillWatch = new System.Diagnostics.Stopwatch();

        public Body Pilot { get; private set; }
        public Rigidbody2D Chassis { get; private set; }
        public MechVisual Visual { get; private set; }
        public MechStats Stats { get; private set; }
        public MechLoadout Loadout { get; private set; }
        public Vector2 HullSize { get; private set; } = Size;
        public PilotVisualProxy PilotProxy => Visual ? Visual.PilotProxy : null;
        public bool Grounded { get; private set; }
        public bool ThrustActive { get; private set; }
        public bool CeilingBlocked { get; private set; }
        public string CeilingReason { get; private set; } = "";
        public bool DrillHeld { get; private set; }
        public bool DrillContact { get; private set; }
        public float HoldTime => holdTime;
        public float VelocityX => Chassis ? Chassis.velocity.x : 0f;
        public float VelocityY => Chassis ? Chassis.velocity.y : 0f;
        public Vector2 AimPoint => lastAimPoint;
        public Vector2? SafePoint => safeValid ? safeSpot : (Vector2?)null;

        // Metrics are observation only; gameplay never reads them.
        public int DrillHits => drillHits;
        public int Shots => shots;
        public int BrokenBlocks => brokenBlocks;
        public int DrillPulses => drillPulses;
        public int LastPulseCandidates => lastPulseCandidates;
        public int LastPulseProcessed => lastPulseProcessed;
        public int LastPulseBroken => lastPulseBroken;
        public float LastDrillMs => lastDrillMs;
        public float AverageDrillMs => drillSamples > 0 ? totalDrillMs / drillSamples : 0f;
        public int LastBlockId => lastBlockId;
        public float LastBlockHealth => lastBlockHealth;
        public string LastBlockName => lastBlockName;
        public int LastBreakId => lastBreakId;
        public float LastBreakHealth => lastBreakHealth;
        public float LastBreakMs => lastBreakMs;
        public string LastHitKind { get; private set; } = "";
        public string LastHitTarget { get; private set; } = "";
        public float LastHitDamage { get; private set; }
        public string LeftArmId => Stats?.LeftArm != null ? Stats.LeftArm.Id : "none";
        public string RightArmId => Stats?.RightArm != null ? Stats.RightArm.Id : "none";

        private readonly Dictionary<Renderer, bool> renderStates = new Dictionary<Renderer, bool>();
        private readonly Dictionary<Rigidbody2D, bool> physicsStates = new Dictionary<Rigidbody2D, bool>();
        private readonly Dictionary<Collider2D, bool> colliderStates = new Dictionary<Collider2D, bool>();
        private readonly Dictionary<Animator, bool> animatorStates = new Dictionary<Animator, bool>();
        private readonly Dictionary<long, float> firstHitTime = new Dictionary<long, float>();
        private readonly GridCell[] drillCells = new GridCell[MaxDrillCells];
        private readonly WorldDrillGrid drillGrid = new WorldDrillGrid();
        private readonly ContactPoint2D[] contacts = new ContactPoint2D[32];
        private readonly float[] nextArmAction = new float[2];
        private readonly bool[] armHeld = new bool[2];
        private readonly bool[] armContact = new bool[2];
        private readonly bool[] beamActive = new bool[2];
        private readonly float[] beamLengths = new float[2];
        private BoxCollider2D hull;
        private PhysicsMaterial2D material;
        private MechAudio audio;
        private int sceneDepth;
        private bool originalForceNoMovement;
        private Vector2 pilotSize = new Vector2(2.2f, 4.6f);
        private float drive, motorVelocity, holdTime, nextSafeCheck, nextBreakLog;
        private bool jumpQueued, requireRelease, thrustRequested, prevGrounded;
        private float prevVelocityY;

        private Vector2 safeSpot;
        private bool safeValid;
        private Vector2 centerSafe;
        private bool centerSafeValid;
        private Vector2 lastAimPoint;

        private int drillHits, shots, brokenBlocks, drillPulses;
        private int lastPulseCandidates, lastPulseProcessed, lastPulseBroken;
        private float lastDrillMs, totalDrillMs;
        private int drillSamples;
        private int lastBlockId;
        private float lastBlockHealth;
        private string lastBlockName = "";
        private int lastBreakId;
        private float lastBreakHealth;
        private float lastBreakMs;

        private static readonly int GroundMask = LayerMask.GetMask("Ground");
        private static readonly int EntityMask = LayerMask.GetMask("Ground", "Descriptor");
        private static readonly int BodyMask = LayerMask.GetMask("Body", "Limb");

        public static bool FindSpawn(Vector2 origin, out Vector2 position)
        {
            return FindSpawn(origin, Size, out position);
        }

        public static bool FindSpawn(Vector2 origin, Vector2 size, out Vector2 position)
        {
            // Never excavate terrain to force a summon. Find an actual ground surface near the
            // player's level, then validate the full hull box at that landing height.
            float[] xs = { 5f, -5f, 7f, -7f, 9f, -9f, 11f, -11f, 13f, -13f, 15f, -15f, 17f, -17f, 19f, -19f, 2f, -2f, 3.5f, -3.5f, 0f };
            float[] ys = { 0f, -2f, 2f, -4f, 4f, -6f, -8f, 6f, 8f };
            foreach (float dx in xs)
            foreach (float dy in ys)
            {
                Vector2 above = origin + new Vector2(dx, dy + 8f);
                var hit = Physics2D.Raycast(above, Vector2.down, 55f, GroundMask);
                if (!hit) continue;
                var landing = hit.point + Vector2.up * (size.y * 0.5f + 0.1f);
                if (Mathf.Abs(landing.y - origin.y) > 28f) continue;
                if (Physics2D.OverlapBox(landing, size + Vector2.one * 0.3f, 0, GroundMask)) continue;
                if (Physics2D.OverlapBox(landing, new Vector2(size.x - 0.8f, size.y - 0.8f), 0, BodyMask)) continue;
                position = landing;
                Plugin.LogInfo("SPAWN_STRICT pos=" + landing + " origin=" + origin + " size=" + size);
                return true;
            }
            // A clear air column just above the player is the only fallback. We no longer
            // throw the mech into a distant deep pit to make summoning appear successful.
            foreach (float dx in new[] { 0f, 4f, -4f, 7f, -7f, 10f, -10f })
            foreach (float dy in new[] { 2f, 4f, 6f, 8f })
            {
                var candidate = origin + new Vector2(dx, dy);
                if (Physics2D.OverlapBox(candidate, size + Vector2.one * 0.25f, 0, GroundMask)) continue;
                if (Physics2D.OverlapBox(candidate, new Vector2(size.x - 0.8f, size.y - 0.8f), 0, BodyMask)) continue;
                if (!Physics2D.Raycast(candidate, Vector2.down, 20f, GroundMask)) continue;
                position = candidate;
                Plugin.LogInfo("SPAWN_AIR_SHORT pos=" + candidate + " origin=" + origin + " size=" + size);
                return true;
            }
            position = origin;
            Plugin.LogInfo("SPAWN_FAILED origin=" + origin + " size=" + size + " reason=no_clear_ground");
            return false;
        }

        private void Awake()
        {
            sceneDepth = WorldGeneration.world ? WorldGeneration.world.biomeDepth : -1;
            var player = PlayerCamera.main ? PlayerCamera.main.body : null;
            Loadout = MechLoadoutStore.Get(player);
            Loadout.Normalize(out _);
            Stats = MechCatalog.Combine(Loadout);
            HullSize = new Vector2(Stats.HullW, Stats.HullH);
            Chassis = gameObject.AddComponent<Rigidbody2D>();
            Chassis.mass = Mathf.Max(30f, Stats.Mass);
            Chassis.gravityScale = 1f;
            Chassis.constraints = RigidbodyConstraints2D.FreezeRotation;
            Chassis.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            Chassis.interpolation = RigidbodyInterpolation2D.Interpolate;
            hull = gameObject.AddComponent<BoxCollider2D>();
            hull.size = HullSize;
            material = new PhysicsMaterial2D("Wayfarer hull") { friction = 0.15f, bounciness = 0f };
            hull.sharedMaterial = material;
            IgnorePilotCollisions();
            Visual = gameObject.AddComponent<MechVisual>();
            Visual.Build(transform, Loadout);
            audio = gameObject.AddComponent<MechAudio>();
            audio.Build();
            Plugin.LogInfo("MECH_CREATED sceneDepth=" + sceneDepth + " hull=" + HullSize + " loadout=" + Loadout.Summary);
        }

        private void IgnorePilotCollisions()
        {
            var body = PlayerCamera.main ? PlayerCamera.main.body : null;
            if (!body) return;
            foreach (var c in body.GetComponentsInChildren<Collider2D>(true))
                if (c && hull) Physics2D.IgnoreCollision(hull, c);
        }

        /// <summary>
        /// Rebuild the visual for an assembly draft. The mech must be unoccupied and
        /// stationary; the hull clearance is re-checked before the collider size changes.
        /// </summary>
        public bool ApplyPreviewLoadout(MechLoadout loadout, Body previewPilot, out string reason)
        {
            reason = null;
            if (Pilot) { reason = "请先下机"; return false; }
            if (loadout == null) { reason = "空配置"; return false; }
            if (!loadout.Normalize(out reason)) reason = null;
            var stats = MechCatalog.Combine(loadout);
            Vector2 newSize = new Vector2(stats.HullW, stats.HullH);
            // The parked mech is allowed to touch its supporting floor. Check a slightly
            // inset volume above the feet for walls/ceilings instead of rejecting every
            // assembly while the mech is standing on the ground.
            Vector2 clearance = new Vector2(Mathf.Max(1f, newSize.x - 0.8f), Mathf.Max(1f, newSize.y - 0.8f));
            Vector2 clearanceCenter = (Vector2)transform.position + Vector2.up * 0.2f;
            if (Physics2D.OverlapBox(clearanceCenter, clearance, 0f, GroundMask))
            {
                reason = "新机体净空不足，请移到更开阔的位置";
                return false;
            }
            if (Physics2D.OverlapBox(clearanceCenter, clearance * 0.9f, 0f, BodyMask))
            {
                reason = "附近有角色挡住部署净空";
                return false;
            }
            if (Visual)
            {
                Visual.DestroyVisual();
                Destroy(Visual);
                Visual = null;
            }
            Loadout = loadout.Clone();
            Stats = stats;
            HullSize = newSize;
            if (hull) hull.size = HullSize;
            if (Chassis) Chassis.mass = Mathf.Max(30f, Stats.Mass);
            Visual = gameObject.AddComponent<MechVisual>();
            Visual.Build(transform, Loadout);
            if (previewPilot) Visual.AttachPilot(previewPilot);
            Plugin.LogInfo("MECH_PREVIEW_LOADOUT " + Loadout.Summary + " hull=" + HullSize);
            return true;
        }

        public void DetachPreviewPilot()
        {
            Visual?.DetachPilot();
        }

        public void Enter(Body body)
        {
            if (Pilot || !body || !body.conscious || !Plugin.InputReady()) return;
            Pilot = body;
            originalForceNoMovement = body.forceNoMovement;
            body.forceNoMovement = true;
            body.moveDir = Vector2.zero;
            CapturePilotSize(body);
            // Build the display proxy while the source renderers still expose their
            // real enabled state (helmet/hair layers), then hide the physical body.
            Visual?.AttachPilot(body);
            foreach (var r in body.GetComponentsInChildren<Renderer>(true)) { renderStates[r] = r.enabled; r.enabled = false; }
            foreach (var r in body.GetComponentsInChildren<Rigidbody2D>(true)) { physicsStates[r] = r.simulated; r.simulated = false; }
            foreach (var c in body.GetComponentsInChildren<Collider2D>(true)) { colliderStates[c] = c.enabled; c.enabled = false; }
            foreach (var a in body.GetComponentsInChildren<Animator>(true)) { animatorStates[a] = a.enabled; a.enabled = false; }
            IgnorePilotCollisions();
            FollowPilot();
            ResetControls(true);
            safeValid = false;
            centerSafeValid = false;
            nextSafeCheck = 0f;
            firstHitTime.Clear();
            for (int i = 0; i < 2; i++) { nextArmAction[i] = 0f; armHeld[i] = false; armContact[i] = false; beamActive[i] = false; }
            Plugin.Tell("驾驶已接管：长按跳跃喷气 / 左键左臂 / 右键右臂 / F 下机 / H 喇叭");
            Debug.Log("[Wayfarer] PILOT_ENTER size=" + pilotSize + " proxy=" + (PilotProxy != null));
        }

        private void CapturePilotSize(Body body)
        {
            pilotSize = new Vector2(2.2f, 4.6f);
            var cols = body.GetComponentsInChildren<Collider2D>(true);
            Vector2 best = Vector2.zero;
            foreach (var c in cols)
            {
                if (!c || !c.enabled) continue;
                var s = c.bounds.size;
                if (s.x > best.x && s.y > best.y) best = s;
            }
            if (best.x > 0.3f && best.y > 0.3f)
                pilotSize = new Vector2(Mathf.Clamp(best.x, 0.8f, 3.2f), Mathf.Clamp(best.y, 2.0f, 6.0f));
        }

        public bool Exit(bool emergency)
        {
            return ExitInternal(emergency, true, out _);
        }

        /// <summary>
        /// Lifecycle exit. When allowUnsafe is false (the save path), the pilot remains
        /// protected inside the mech if no terrain spot can be validated. The caller must
        /// not destroy the mech in that case.
        /// </summary>
        public bool ExitForLifecycle(string reason, bool allowUnsafe)
        {
            if (!Pilot) return true;
            bool ok = ExitInternal(true, allowUnsafe, out string method);
            Plugin.LogInfo("LIFECYCLE_EXIT reason=" + reason + " ok=" + ok + " method=" + method);
            return ok;
        }

        /// <summary>
        /// The save path may have to keep the pilot inside the mech when no terrain
        /// spot validates. Restore the original body-level movement flag before the
        /// character save so a saved pilot never loads permanently frozen. The body
        /// remains hidden and skip-patched while it is still a pilot.
        /// </summary>
        public void RestorePilotStateForSaveHold()
        {
            if (!Pilot) return;
            Pilot.forceNoMovement = originalForceNoMovement;
            Pilot.moveDir = Vector2.zero;
            Plugin.LogInfo("SAFE_SAVE_HOLD_STATE_RESTORED forceNoMovement=" + Pilot.forceNoMovement);
        }

        private bool ExitInternal(bool emergency, bool allowUnsafe, out string method)
        {
            method = "none";
            if (!Pilot) return true;
            Vector2 exit;
            bool found;
            if (!emergency)
            {
                // F is the player's decision, including in mid-air. Prefer body clearance
                // beside the chassis, but never require a floor or reject the exit.
                exit = MechSafeExit.ManualExit(transform.position, pilotSize, Visual ? Visual.Facing : 1);
                found = true;
                method = "manual";
            }
            else
            {
                found = MechSafeExit.FindNearby(transform.position, pilotSize, out exit);
                method = "nearby";
                if (!found && safeValid && MechSafeExit.IsStandingSpot(safeSpot, pilotSize))
                {
                    exit = safeSpot; found = true; method = "cached";
                }
                if (!found && centerSafeValid && MechSafeExit.IsStandingSpot(centerSafe, pilotSize))
                {
                    exit = centerSafe; found = true; method = "center";
                }
                if (!found)
                {
                    found = MechSafeExit.FindForced(transform.position, pilotSize, out exit, out method);
                }
                if (!found && !allowUnsafe)
                {
                    method = "held";
                    Plugin.LogError("SAFE_EXIT_HOLD: no validated terrain spot; keeping pilot inside protected mech");
                    return false;
                }
                if (!found)
                {
                    exit = transform.position;
                    method = "unsafe-current";
                    Plugin.LogError("SAFE_EXIT_FAILED: no validated terrain spot; restoring pilot at mech position");
                }
                Plugin.LogInfo("SAFE_EXIT method=" + method + " pos=" + exit + " mech=" + (Vector2)transform.position);
                if (!found)
                    Plugin.Tell("未找到安全落脚点，已在当前位置退出 / No safe ground found; exited at current position");
                else if (method != "nearby")
                    Plugin.Tell("机甲已安全回收，驾驶者已转移到安全地面 / Landed safely before exit");
            }

            var body = Pilot;
            Visual?.DetachPilot();
            body.transform.position = exit;
            foreach (var kv in renderStates) if (kv.Key) kv.Key.enabled = kv.Value;
            foreach (var kv in colliderStates) if (kv.Key) kv.Key.enabled = kv.Value;
            foreach (var kv in physicsStates)
            {
                if (!kv.Key) continue;
                kv.Key.simulated = kv.Value;
                kv.Key.velocity = Vector2.zero;
                kv.Key.angularVelocity = 0f;
            }
            foreach (var kv in animatorStates) if (kv.Key) kv.Key.enabled = kv.Value;
            body.forceNoMovement = originalForceNoMovement;
            body.moveDir = Vector2.zero;
            Pilot = null;
            renderStates.Clear();
            colliderStates.Clear();
            physicsStates.Clear();
            animatorStates.Clear();
            firstHitTime.Clear();
            ResetControls(true);
            ThrustActive = false;
            DrillContact = false;
            DrillHeld = false;
            for (int i = 0; i < 2; i++) SetArmVisual(i, false, false, false, 0f);
            if (audio) { audio.SetThrust(false); audio.SetDrill(false); }
            if (Visual) Visual.SetMotion(0f, 0f, true, false);
            if (Chassis) Chassis.velocity = new Vector2(0f, Chassis.velocity.y);
            Debug.Log("[Wayfarer] PILOT_EXIT method=" + method + " pos=" + exit);
            return true;
        }

        private void FollowPilot()
        {
            if (!Pilot) return;
            Pilot.transform.position = transform.position + Vector3.up * 0.5f;
            Pilot.moveDir = Vector2.zero;
            // Keep the real face looking in the mech's travel direction instead of
            // reusing a stale "peeking behind" look from the moment the player boarded.
            if (Visual)
            {
                Pilot.isRight = Visual.Facing > 0;
                Vector2 look = (Vector2)transform.position + Vector2.right * (Visual.Facing * 8f);
                Pilot.targetLookPos = look;
                Pilot.overrideLookPos = look;
                Pilot.overrideLookTime = 0.12f;
            }
            // Other game scripts can re-enable renderers; keep the cabin occupant hidden.
            foreach (var r in renderStates.Keys) if (r) r.enabled = false;
        }

        private void ResetControls(bool cancelJump)
        {
            drive = 0f;
            motorVelocity = 0f;
            holdTime = 0f;
            thrustRequested = false;
            ThrustActive = false;
            DrillHeld = false;
            DrillContact = false;
            if (cancelJump) jumpQueued = false;
            requireRelease = Input.GetKey(KeyBinds.GetBind("jump"));
        }

        private void Update()
        {
            var w = WorldGeneration.world;
            if (!w || sceneDepth != w.biomeDepth || w.generatingWorld)
            {
                Plugin.LogInfo("MECH_SCENE_EXIT depth=" + sceneDepth + "->" + (w ? w.biomeDepth : -1));
                ExitForLifecycle("scene", true);
                Destroy(gameObject);
                return;
            }
            if (!Pilot)
            {
                Grounded = CheckGrounded();
                if (Visual) Visual.SetMotion(0f, VelocityY, Grounded, false);
                if (audio) { audio.SetThrust(false); audio.SetDrill(false); }
                return;
            }
            if (!Pilot.conscious)
            {
                Plugin.Tell("驾驶者失去意识，紧急下机 / Pilot unconscious, emergency exit");
                Exit(true);
                return;
            }

            bool active = Plugin.InputReady();
            if (!active)
            {
                ResetControls(true);
                if (audio) { audio.SetThrust(false); audio.SetDrill(false); }
                if (Visual) Visual.SetMotion(VelocityX, VelocityY, Grounded, false);
                for (int i = 0; i < 2; i++) SetArmVisual(i, false, false, false, 0f);
                return;
            }

            drive = (Input.GetKey(KeyBinds.GetBind("right")) ? 1f : 0f) -
                    (Input.GetKey(KeyBinds.GetBind("left")) ? 1f : 0f);

            bool jumpHeld = Input.GetKey(KeyBinds.GetBind("jump"));
            bool jumpPressed = Input.GetKeyDown(KeyBinds.GetBind("jump"));
            if (!jumpHeld)
            {
                holdTime = 0f;
                thrustRequested = false;
                requireRelease = false;
            }
            else if (requireRelease)
            {
                holdTime = 0f;
                thrustRequested = false;
            }
            else
            {
                if (jumpPressed)
                {
                    holdTime = 0f;
                    jumpQueued = true;
                }
                else
                {
                    holdTime += Time.deltaTime;
                }
                if (holdTime >= MechSettings.HoldThreshold.Value) thrustRequested = true;
            }

            var aim = (Vector2)Camera.main.ScreenToWorldPoint(Input.mousePosition);
            lastAimPoint = aim;
            float dx = aim.x - transform.position.x;
            int facing = Mathf.Abs(dx) > 0.12f ? (dx >= 0f ? 1 : -1) : Visual.Facing;
            Visual.SetFacing(facing);
            for (int i = 0; i < 2; i++)
            {
                var pivot = Visual.ArmPivots[i];
                if (!pivot) continue;
                Visual.SetArmAim(i, AimAngle(aim, pivot.position, facing));
            }

            bool pointerOverUI = UIUtil.IsPointerOverUIElement();
            bool nearHeld = !pointerOverUI && Input.GetMouseButton(0);
            bool farHeld = !pointerOverUI && Input.GetMouseButton(1);
            bool anyDrill = false, anyContact = false;
            for (int i = 0; i < 2; i++)
            {
                var part = i == 0 ? Stats.LeftArm : Stats.RightArm;
                bool held = (i == 0 ? nearHeld : farHeld) && part != null && part.Behavior != ArmBehavior.None;
                armHeld[i] = held;
                if (!held || !Visual.ArmPivots[i] || !Visual.ArmTips[i]) { armContact[i] = false; beamActive[i] = false; SetArmVisual(i, false, false, false, 0f); continue; }
                Vector2 tip = Visual.ArmTips[i].position;
                Vector2 pivot = Visual.ArmPivots[i].position;
                switch (part.Behavior)
                {
                    case ArmBehavior.Drill:
                        if (Time.time >= nextArmAction[i])
                        {
                            nextArmAction[i] = Time.time + Mathf.Max(0.03f, MechSettings.DrillInterval.Value * part.CooldownMul);
                            armContact[i] = DrillArm(i, pivot, tip, aim, part);
                        }
                        anyDrill |= part.Behavior == ArmBehavior.Drill && held;
                        anyContact |= armContact[i];
                        break;
                    case ArmBehavior.Cannon:
                        if (Time.time >= nextArmAction[i])
                        {
                            nextArmAction[i] = Time.time + 0.23f * part.CooldownMul;
                            var hit = MechCombat.Cannon(pivot, tip, aim,
                                MechSettings.DrillRange.Value * 1.7f * part.RangeMul, Pilot, transform,
                                100f * part.TerrainMul, 180f * part.EntityMul, 85f * part.EntityMul);
                            Visual.PulseFire(i);
                            Sound.Play("rifleshot", tip, twoDimensional: false, pitchShift: false);
                            armContact[i] = hit.hit && !hit.wall;
                        }
                        else armContact[i] = false;
                        break;
                    case ArmBehavior.Melee:
                        if (Time.time >= nextArmAction[i])
                        {
                            nextArmAction[i] = Time.time + 0.12f * part.CooldownMul;
                            armContact[i] = MeleeArm(i, pivot, tip, aim, part);
                            anyDrill |= held;
                        }
                        else armContact[i] = false;
                        anyContact |= armContact[i];
                        break;
                    case ArmBehavior.BeamDrill:
                        if (Time.time >= nextArmAction[i])
                        {
                            nextArmAction[i] = Time.time + 0.09f * part.CooldownMul;
                            var hit = MechCombat.Beam(pivot, tip, aim,
                                MechSettings.DrillRange.Value * 2.1f * part.RangeMul, Pilot, transform,
                                MechSettings.DrillTerrainDamage.Value * 0.55f * part.TerrainMul,
                                MechSettings.DrillEntityDamage.Value * 0.9f * part.EntityMul, out float len);
                            beamActive[i] = true;
                            beamLengths[i] = len;
                            SetArmVisual(i, true, hit.hit, false, len);
                            armContact[i] = hit.hit;
                            if (audio) audio.SetDrill(true);
                        }
                        else
                        {
                            armContact[i] = beamActive[i];
                        }
                        anyDrill |= beamActive[i];
                        anyContact |= armContact[i];
                        break;
                }
                if (part.Behavior == ArmBehavior.BeamDrill)
                    SetArmVisual(i, held, armContact[i], false, beamLengths[i]);
                else
                    SetArmVisual(i, held, armContact[i], part.Behavior == ArmBehavior.Cannon, 0f);
            }
            DrillHeld = anyDrill;
            DrillContact = anyContact;
            if (audio) audio.SetDrill(anyDrill);

            if (Input.GetKeyDown(Plugin.HornKey.Value) && Loadout.HornId != "none")
            {
                Visual.Honk();
                Sound.Play("turretsee", transform.position, twoDimensional: false, pitchShift: false);
                Plugin.LogInfo("MECH_HORN id=" + Loadout.HornId);
            }

            if (Grounded && Time.time >= nextSafeCheck)
            {
                nextSafeCheck = Time.time + MechSettings.SafeSpotInterval.Value;
                UpdateSafeSpot();
            }

            if (Grounded && !prevGrounded && prevVelocityY < -4f)
                Visual.NotifyLanded(Mathf.InverseLerp(4f, 40f, -prevVelocityY));
            prevGrounded = Grounded;
            prevVelocityY = VelocityY;

            Visual.SetMotion(VelocityX, VelocityY, Grounded, ThrustActive);
            if (audio) audio.SetThrust(ThrustActive, Mathf.Clamp01(Mathf.Abs(VelocityY) / 20f + 0.35f));
        }

        private void SetArmVisual(int index, bool held, bool contact, bool fire, float beamLength)
        {
            if (!Visual) return;
            Visual.SetArmActivity(index, held, contact);
            if (Stats != null && (index == 0 ? Stats.LeftArm : Stats.RightArm)?.Behavior == ArmBehavior.BeamDrill)
                Visual.SetBeam(index, held, beamLength > 0f ? beamLength * MechArt.Ppu : 0f);
        }

        private static float AimAngle(Vector2 aim, Vector2 pivot, int facing)
        {
            Vector2 d = aim - pivot;
            float localX = d.x * facing;
            if (localX < 0.15f) localX = 0.15f;
            return Mathf.Atan2(d.y, localX) * Mathf.Rad2Deg;
        }

        private void UpdateSafeSpot()
        {
            centerSafeValid = MechSafeExit.IsStandingSpot(transform.position, pilotSize);
            if (centerSafeValid) centerSafe = transform.position;
            if (MechSafeExit.FindNearby(transform.position, pilotSize, out var side))
            {
                safeSpot = side;
                safeValid = true;
            }
        }

        private void FixedUpdate()
        {
            if (!Pilot) return;
            bool active = Plugin.InputReady();
            Grounded = CheckGrounded();
            CeilingBlocked = CheckCeiling();
            Vector2 v = Chassis.velocity;

            float targetX = active ? drive * MechSettings.MoveSpeed.Value * Stats.MoveMul : 0f;
            bool reversing = targetX * motorVelocity < 0f;
            bool braking = reversing || Mathf.Abs(targetX) < Mathf.Abs(motorVelocity);
            float acceleration = braking ? MechSettings.HorizontalBrake.Value : MechSettings.HorizontalAccel.Value;
            // Reversals decelerate to zero first instead of snapping into the opposite drive.
            // Integrate the drive command separately: contact friction may cancel a
            // small per-tick velocity, but must not erase all accumulated acceleration.
            motorVelocity = Mathf.MoveTowards(motorVelocity, reversing ? 0f : targetX, acceleration * Time.fixedDeltaTime);
            v.x = motorVelocity;

            bool wantThrust = active && thrustRequested && !CeilingBlocked;
            ThrustActive = wantThrust;

            if (jumpQueued)
            {
                if (active && Grounded) v.y = MechSettings.JumpSpeed.Value * Stats.JumpMul;
                jumpQueued = false;
            }
            if (wantThrust)
            {
                v.y += (MechSettings.ThrustNetAccel.Value * Stats.ThrustMul - Physics2D.gravity.y * Chassis.gravityScale) * Time.fixedDeltaTime;
                float maxRise = MechSettings.MaxRiseSpeed.Value * Stats.RiseMul;
                if (v.y > maxRise) v.y = maxRise;
            }
            if (CeilingBlocked && v.y > 0f) v.y = 0f;
            float maxFall = MechSettings.MaxFallSpeed.Value * Stats.FallMul;
            if (v.y < -maxFall) v.y = -maxFall;
            Chassis.velocity = v;
            FollowPilot();
        }

        private bool CheckGrounded()
        {
            Vector2 center = transform.position;
            Vector2 feet = center + Vector2.down * (HullSize.y * 0.5f - 0.1f);
            if (Physics2D.OverlapBox(feet, new Vector2(HullSize.x - 0.5f, 0.25f), 0f, GroundMask)) return true;
            var hit = Physics2D.BoxCast(center,
                new Vector2(HullSize.x - 0.6f, HullSize.y - 0.3f), 0f, Vector2.down, 0.45f, GroundMask);
            return hit;
        }

        /// <summary>
        /// Short, normal-filtered contact test. Side walls and sloped ceilings must not
        /// extinguish thrust; only a surface whose normal points down onto the hull top
        /// counts as a ceiling. This is intentionally much shorter than the old 1.7-unit
        /// overlap probe.
        /// </summary>
        private bool CheckCeiling()
        {
            Vector2 center = transform.position;
            CeilingReason = "";
            // A grounded mech must not report a false ceiling from the floor contact.
            // The check is only meaningful while airborne (jump/thrust).
            if (Grounded) return false;
            if (hull)
            {
                int count = hull.GetContacts(contacts);
                for (int i = 0; i < count && i < contacts.Length; i++)
                {
                    if (contacts[i].normal.y < -0.45f && contacts[i].point.y > center.y + HullSize.y * 0.5f - 0.35f)
                    {
                        CeilingReason = "contact";
                        return true;
                    }
                }
            }
            float halfW = HullSize.x * 0.5f - 0.25f;
            // A short overlap catches a flat slab whose tilemap normal is ambiguous.
            Vector2 overlapCenter = center + Vector2.up * (HullSize.y * 0.5f + 0.22f);
            if (Physics2D.OverlapBox(overlapCenter, new Vector2(HullSize.x - 0.5f, 0.4f), 0f, GroundMask))
            {
                CeilingReason = "overlap";
                return true;
            }
            // The medium rays stop thrust before the hull visibly penetrates a slab;
            // a contact-only test can miss the one frame at high rise speed and bounce.
            for (int i = -1; i <= 1; i++)
            {
                Vector2 origin = center + new Vector2(i * halfW, HullSize.y * 0.5f - 0.05f);
                var hit = Physics2D.Raycast(origin, Vector2.up, 1.45f, GroundMask);
                if (hit && hit.normal.y < 0.2f)
                {
                    CeilingReason = "ray";
                    return true;
                }
            }
            var box = Physics2D.BoxCast(center, new Vector2(HullSize.x - 0.5f, HullSize.y - 0.2f), 0f,
                Vector2.up, 0.24f, GroundMask);
            if (box && box.normal.y < 0.2f)
            {
                CeilingReason = "boxcast";
                return true;
            }
            return false;
        }

        private bool MeleeArm(int index, Vector2 pivot, Vector2 tip, Vector2 aim, MechPartDef part)
        {
            if (!Pilot || !Plugin.Ready) return false;
            MechCombat.ResolveMuzzle(pivot, tip, out var origin, out bool blocked);
            if (blocked) return false;
            Vector2 toAim = aim - origin;
            if (toAim.sqrMagnitude < 0.16f) toAim = (Vector2)transform.right * Visual.Facing;
            if (toAim.sqrMagnitude < 0.001f) return false;
            Vector2 dir = toAim.normalized;
            float range = Mathf.Max(2.2f, MechSettings.DrillEntityRange.Value * 0.8f * part.RangeMul);
            var hit = MechCombat.Cast(origin, dir, range, transform, Pilot);
            if (hit.building != null)
            {
                float dmg = (hit.building.animal ? MechSettings.DrillAnimalDamage.Value : MechSettings.DrillEntityDamage.Value) * part.EntityMul;
                MechCombat.DamageBuilding(hit, dmg, dmg);
                return true;
            }
            if (hit.body != null && !MechCombat.IsFriendly(hit.body, Pilot))
            {
                MechCombat.DamageBody(hit, MechSettings.DrillEntityDamage.Value * part.EntityMul);
                return true;
            }
            return hit.wall;
        }

        private bool DrillArm(int index, Vector2 pivot, Vector2 tip, Vector2 aim, MechPartDef part)
        {
            if (!Pilot || !Plugin.Ready) return false;
            MechCombat.ResolveMuzzle(pivot, tip, out var origin, out bool blocked);
            Vector2 toAim = aim - origin;
            if (toAim.sqrMagnitude < 0.25f) toAim = (Vector2)transform.right * Visual.Facing;
            if (toAim.sqrMagnitude < 0.001f) return false;
            Vector2 dir = toAim.normalized;
            float range = MechSettings.DrillRange.Value * part.RangeMul;
            if (Vector2.Distance(transform.position, origin) >= range) return false;

            DrillWatch.Restart();
            bool entityHit = false;
            var hit = MechCombat.Cast(origin, dir, MechSettings.DrillEntityRange.Value * part.RangeMul, transform, Pilot);
            if (hit.building != null)
            {
                float dmg = (hit.building.animal ? MechSettings.DrillAnimalDamage.Value : MechSettings.DrillEntityDamage.Value) * part.EntityMul;
                MechCombat.DamageBuilding(hit, dmg, dmg);
                entityHit = true;
            }
            else if (hit.body != null && !MechCombat.IsFriendly(hit.body, Pilot))
            {
                MechCombat.DamageBody(hit, MechSettings.DrillEntityDamage.Value * part.EntityMul);
                entityHit = true;
            }

            var w = WorldGeneration.world;
            if (!w || !w.worldExists) return entityHit;
            drillGrid.W = w;
            int maxCells = Mathf.Clamp(MechSettings.DrillMaxCells.Value, 1, MaxDrillCells);
            int depth = Mathf.Clamp(MechSettings.DrillDepth.Value, 1, 4);
            int planned = DrillShape.Plan(
                transform.position.x, transform.position.y,
                w.halfWidth, w.halfHeight,
                HullSize.x, HullSize.y, dir.x, dir.y,
                range, depth, maxCells, drillGrid, drillCells, out int candidates);

            lastPulseCandidates = candidates;
            lastPulseProcessed = planned;
            lastPulseBroken = 0;
            drillPulses++;
            int brokenThisPulse = 0;
            float terrainDamage = MechSettings.DrillTerrainDamage.Value * part.TerrainMul;
            int softBudget = Mathf.Clamp(MechSettings.DrillMaxSoftBreaks.Value, 1, 32);
            int softBreaks = 0;

            bool oldSuppress = NativeDrillSound.Suppress;
            NativeDrillSound.Suppress = true;
            try
            {
                for (int i = 0; i < planned; i++)
                {
                    var cell = drillCells[i];
                    int before = w.GetBlock(new Vector2Int(cell.X, cell.Y));
                    if (before == 0) continue;
                    var info = w.GetBlockInfo((ushort)before);
                    if (info == null || info.health <= 0f || info.health >= DrillShape.IndestructibleHealth) continue;
                    if (info.health <= terrainDamage && softBreaks >= softBudget) continue;
                    long key = ((long)cell.X << 32) ^ (uint)cell.Y;
                    float first;
                    if (!firstHitTime.TryGetValue(key, out first))
                    {
                        first = Time.time;
                        if (firstHitTime.Count > 256) firstHitTime.Clear();
                        firstHitTime[key] = first;
                    }
                    lastBlockId = before;
                    lastBlockHealth = info.health;
                    lastBlockName = info.name;
                    w.DamageBlock(new Vector2Int(cell.X, cell.Y), terrainDamage, hitSound: false, bonusMetal: false, ignoreLoot: false);
                    if (w.GetBlock(new Vector2Int(cell.X, cell.Y)) == 0)
                    {
                        brokenThisPulse++;
                        if (info.health <= terrainDamage) softBreaks++;
                        lastBreakId = before;
                        lastBreakHealth = info.health;
                        lastBreakMs = (Time.time - first) * 1000f;
                        brokenBlocks++;
                        firstHitTime.Remove(key);
                        if (info.health >= 1000f || Time.time >= nextBreakLog)
                        {
                            nextBreakLog = Time.time + 0.25f;
                            Plugin.LogInfo("DRILL_BREAK id=" + before + " name=" + info.name + " hp=" + info.health +
                                           " ms=" + lastBreakMs.ToString("0") + " cell=" + cell.X + "," + cell.Y);
                        }
                    }
                }
            }
            finally
            {
                NativeDrillSound.Suppress = oldSuppress;
                DrillWatch.Stop();
            }
            lastDrillMs = (float)DrillWatch.Elapsed.TotalMilliseconds;
            totalDrillMs += lastDrillMs;
            drillSamples++;
            lastPulseBroken = brokenThisPulse;
            if (planned > 0 || entityHit) drillHits++;
            return planned > 0 || entityHit;
        }

        public void NoteShot(MechHit hit)
        {
            shots++;
            LastHitKind = hit.hit ? hit.kind : "none";
            LastHitTarget = hit.building ? (hit.building.fullName ?? hit.building.id) : hit.body ? "body:" + hit.body.GetInstanceID() : "";
            Plugin.LogInfo("MECH_FIRE hit=" + LastHitKind + " target=" + LastHitTarget + " point=" + hit.point);
        }

        public void NoteTargetHit(string kind, Vector2 point, float damage)
        {
            LastHitKind = kind;
            LastHitDamage = damage;
            Plugin.LogInfo("MECH_TARGET_HIT kind=" + kind + " point=" + point + " damage=" + damage);
        }

        public void NoteBreak(Vector2Int cell, int id, BlockInfo info)
        {
            LastHitKind = "terrain";
            LastHitTarget = info != null ? info.name : id.ToString();
            LastHitDamage = 0f;
            Plugin.LogInfo("MECH_BLOCK_HIT cell=" + cell + " id=" + id + " name=" + (info != null ? info.name : "?"));
        }

        private void OnDestroy()
        {
            Visual?.DetachPilot();
            if (Pilot) Exit(true);
            if (audio) { audio.SetThrust(false); audio.SetDrill(false); }
            if (material) Destroy(material);
            if (Plugin.Mech == this) Plugin.Mech = null;
            Plugin.LogInfo("MECH_DESTROYED");
        }

        private sealed class WorldDrillGrid : IDrillGrid
        {
            public WorldGeneration W;
            public bool InBounds(int x, int y) => W && W.worldExists &&
                x >= 1 && y >= 1 && x < (int)W.width - 1 && y < (int)W.height - 1;
            public int GetBlock(int x, int y) => W ? W.GetBlock(new Vector2Int(x, y)) : 0;
            public float GetBlockHealth(int x, int y)
            {
                if (!W) return 0f;
                int id = W.GetBlock(new Vector2Int(x, y));
                if (id == 0) return 0f;
                var info = W.GetBlockInfo((ushort)id);
                return info != null ? info.health : 0f;
            }
        }
    }
}
