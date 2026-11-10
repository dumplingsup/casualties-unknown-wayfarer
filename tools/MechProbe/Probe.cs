using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using BepInEx;
using Newtonsoft.Json;
using UnityEngine;
using WayfarerMech;
using HarmonyLib;

// Observation only: no artificial passes, key injection or arbitrary code execution.
// Samples every frame in memory and appends an aggregated record every 0.5s.
[BepInPlugin("local.cu.mechprobe", "Mech observation probe", "0.2.0")]
[BepInDependency(Plugin.Guid)]
public class Probe : BaseUnityPlugin
{
    public static Probe Instance;
    private string output;
    private float windowStart;
    private readonly float[] frameTimes = new float[2048];
    private int frameCount;
    private int maxMechY = int.MinValue, minMechY = int.MaxValue;
    private int maxPlayerY = int.MinValue, minPlayerY = int.MaxValue;
    private float maxVy = float.MinValue, minVy = float.MaxValue;
    private int windowThrustFrames, windowSamples;
    private int windowBrokenStart;
    private bool wasPiloting;
    private MechController trackedMech;
    private float pilotStartY, pilotMaxY, pilotMinY, pilotStartTime, pilotMaxVy, pilotMinVy;
    private int pilotStartBroken, pilotThrustFrames;
    private float nextScan;
    private bool fpsApplied;
    private int targetFps;
    private bool autoStart;
    private bool autoStartTried;
    private float titleSince;
    private bool autoPlay;
    private int autoStage;
    private float autoNext;
    private bool autoShot;
    private float nextShot;
    private int shotIndex;
    private bool fakeInput;
    private bool loadSave;
    private bool inspectOnly;
    private bool combatTest;
    private bool ceilingTest;
    private bool highSaveTest;
    private bool noArena;
    private bool perfMode;
    private bool catalogTest;
    private bool lifecycleTest;
    private bool layerTest;
    private int scriptStage;
    private float scriptNext;
    private BuildingEntity combatTarget;
    private float combatHealth;
    private Body friendlyTarget;
    private float friendlyHealth;
    private GameObject ceilingProbeObject;
    private int ceilingTrueFrames;
    private int ceilingThrustFrames;
    private float ceilingMaxY;
    private float ceilingSlabBottom;

    void Awake()
    {
        Instance = this;
        output = Environment.GetEnvironmentVariable("CU_PROBE_DIR");
        if (string.IsNullOrEmpty(output)) { enabled = false; return; }
        Directory.CreateDirectory(output);
        int hz = 20;
        int.TryParse(Environment.GetEnvironmentVariable("CU_PROBE_HZ") ?? "20", out hz);
        int.TryParse(Environment.GetEnvironmentVariable("CU_PROBE_FPS") ?? "0", out targetFps);
        autoStart = Environment.GetEnvironmentVariable("CU_PROBE_AUTOSTART") == "1";
        autoPlay = Environment.GetEnvironmentVariable("CU_PROBE_AUTO") == "1";
        autoShot = Environment.GetEnvironmentVariable("CU_PROBE_AUTOSHOT") == "1";
        fakeInput = Environment.GetEnvironmentVariable("CU_PROBE_FAKEINPUT") == "1";
        loadSave = Environment.GetEnvironmentVariable("CU_PROBE_LOADSAVE") == "1";
        inspectOnly = Environment.GetEnvironmentVariable("CU_PROBE_INSPECT") == "1";
        combatTest = Environment.GetEnvironmentVariable("CU_PROBE_COMBAT") == "1";
        ceilingTest = Environment.GetEnvironmentVariable("CU_PROBE_CEILING") == "1";
        highSaveTest = Environment.GetEnvironmentVariable("CU_PROBE_HIGHSAVE") == "1";
        noArena = Environment.GetEnvironmentVariable("CU_PROBE_NOARENA") == "1";
        perfMode = Environment.GetEnvironmentVariable("CU_PROBE_PERF") == "1";
        catalogTest = Environment.GetEnvironmentVariable("CU_PROBE_CATALOG") == "1";
        lifecycleTest = Environment.GetEnvironmentVariable("CU_PROBE_LIFECYCLE") == "1";
        layerTest = Environment.GetEnvironmentVariable("CU_PROBE_LAYER") == "1";
        ProbeInput.Enabled = fakeInput;
        Logger.LogInfo("Probe observations: " + output + " hz=" + hz + " targetFps=" + targetFps +
                       " autostart=" + autoStart + " auto=" + autoPlay + " autoshot=" + autoShot +
                       " fakeinput=" + fakeInput);
        new Harmony("local.cu.mechprobe").PatchAll(typeof(Probe).Assembly);
    }

    private void Update()
    {
        // The title screen has no PlayerCamera, so the normal probe Tick hook cannot run
        // there. This test-only path enters a normal new run through PreRunScript.StartRun.
        TryAutoStart(Time.unscaledTime);
        AutoShot();
    }

    private void LateUpdate() { ProbeInput.EndFrame(); }

    private void AutoShot()
    {
        if (!autoShot || string.IsNullOrEmpty(output) || !Plugin.Ready) return;
        if (Time.unscaledTime < nextShot) return;
        nextShot = Time.unscaledTime + 2f;
        try
        {
            string path = Path.Combine(output, "shot-auto-" + shotIndex++ + ".png");
            ScreenCapture.CaptureScreenshot(path, 1);
        }
        catch (Exception ex) { Logger.LogWarning("PROBE_SHOT_FAILED " + ex.Message); }
    }

    private System.Collections.IEnumerator AuditSeatedHand(Body body, PilotVisualProxy proxy)
    {
        if (!body || body.limbs == null || body.limbs.Length < 6 || !proxy) yield break;
        var hand = body.limbs[5];
        bool previous = hand.dismembered;
        int count = proxy.HandCount;
        try
        {
            hand.dismembered = true;
            yield return null;
            yield return new WaitForEndOfFrame();
            int expected = previous ? count : count - 1;
            if (proxy.HandCount != expected) Logger.LogError("PROBE_HAND_HIDE_FAIL before=" + count + " after=" + proxy.HandCount);
            else Logger.LogInfo("PROBE_HAND_HIDE_PASS before=" + count + " after=" + proxy.HandCount);
        }
        finally { if (hand) hand.dismembered = previous; }
        yield return null;
        yield return new WaitForEndOfFrame();
        if (proxy && proxy.HandCount == count) Logger.LogInfo("PROBE_HAND_RESTORE_PASS hands=" + count);
        else Logger.LogError("PROBE_HAND_RESTORE_FAIL");
    }

    private void AuditSaveRecord(Body body)
    {
        var provider = new MechLoadoutSaveProvider();
        var original = provider.Capture(body);
        int originalVersion = provider.GetVersion();
        try
        {
            var future = Newtonsoft.Json.Linq.JObject.Parse("{\"schemaVersion\":9,\"body\":\"future_body\",\"opaque\":{\"values\":[1,2,3]}}");
            provider.Restore(body, future, 7, null);
            var captured = provider.Capture(body);
            if (!Newtonsoft.Json.Linq.JToken.DeepEquals(future, captured) || provider.GetVersion() != 7)
                throw new Exception("Future save record was altered");
            MechLoadoutStore.Set(body, MechLoadout.Default());
            captured = provider.Capture(body);
            if ((int)captured["schemaVersion"] != 1 || provider.GetVersion() != 1 || captured["opaque"] != null)
                throw new Exception("Explicit apply did not replace future record");
            provider.Restore(body, new Newtonsoft.Json.Linq.JValue("malformed"), 3, null);
            if ((string)provider.Capture(body) != "malformed" || provider.GetVersion() != 3)
                throw new Exception("Malformed record was lost");
            Logger.LogInfo("PROBE_SAVE_RECORD_PASS futurePayload=True futureEnvelope=True explicitApply=True malformed=True");
        }
        catch (Exception ex) { Logger.LogError("PROBE_SAVE_RECORD_FAIL " + ex); }
        finally { provider.Restore(body, original, originalVersion, null); }
    }

    private void AutoPlay(Body b, MechController m)
    {
        if (!autoPlay || inspectOnly || !Plugin.Ready || b == null) return;
        if (Time.unscaledTime < autoNext) return;
        if (autoStage == 0)
        {
            if (!Plugin.HasBeacon(b)) return;
            if (noArena)
            {
                Logger.LogInfo("PROBE_AUTO_NATURAL_SITE player=" + (Vector2)b.transform.position);
                autoNext = Time.unscaledTime + 0.8f;
                autoStage = 1;
                return;
            }
            if (!TryBuildArena(b, out var playerPos)) { Logger.LogInfo("PROBE_AUTO_NO_ARENA"); autoStage = 99; return; }
            TeleportPlayer(b, playerPos);
            autoNext = Time.unscaledTime + 0.8f;
            autoStage = 1;
            Logger.LogInfo("PROBE_AUTO_ARENA player=" + playerPos);
            return;
        }
        if (autoStage == 1)
        {
            if (m == null)
            {
                Plugin.ToggleSummon();
                autoNext = Time.unscaledTime + 1.0f;
                autoStage = 2;
                Logger.LogInfo("PROBE_AUTO_SUMMON_REQUEST");
            }
            else { autoStage = 2; autoNext = Time.unscaledTime + 0.2f; }
            return;
        }
        if (autoStage == 2)
        {
            m = Plugin.Mech;
            if (m == null) { Logger.LogInfo("PROBE_AUTO_SUMMON_FAILED"); autoStage = 99; return; }
            Vector2 pos = (Vector2)m.transform.position + Vector2.up * 4.8f;
            TeleportPlayer(b, pos);
            autoNext = Time.unscaledTime + 0.5f;
            autoStage = 3;
            Logger.LogInfo("PROBE_AUTO_SUMMONED " + m.transform.position);
            return;
        }
        if (autoStage == 3)
        {
            m = Plugin.Mech;
            if (m == null) { autoStage = 99; return; }
            if (m.Pilot == null) m.Enter(b);
            autoNext = Time.unscaledTime + 1.0f;
            autoStage = m.Pilot != null ? 4 : 98;
            Logger.LogInfo("PROBE_AUTO_ENTER result=" + (m.Pilot != null) + " inputReady=" + Plugin.InputReady());
            return;
        }
        if (autoStage == 4)
        {
            // Stay boarded so screenshots/status can prove the proxy and open cockpit.
            autoNext = Time.unscaledTime + 1f;
            autoStage = 5;
            Logger.LogInfo("PROBE_AUTO_BOARDED pilotProxy=" + (m != null && m.PilotProxy != null && m.PilotProxy.HasFace));
            if (fakeInput && !perfMode)
            {
                scriptStage = Environment.GetEnvironmentVariable("CU_PROBE_ART") == "1" ? 90 : combatTest ? 30 : ceilingTest ? 60 : lifecycleTest ? 70 : layerTest ? 80 : 10;
                scriptNext = Time.unscaledTime + 0.5f;
            }
            else if (perfMode)
            {
                Logger.LogInfo("PROBE_PERF_IDLE_BOARDED pilotProxy=" + (m != null && m.PilotProxy != null && m.PilotProxy.HasFace));
            }
        }
        if (ceilingTest && fakeInput && autoStage >= 5 && m != null && m.Pilot != null)
        {
            CeilingScript(m);
            return;
        }
        if (combatTest && fakeInput && autoStage >= 5 && m != null && m.Pilot != null)
        {
            CombatScript(m);
            return;
        }
        if (lifecycleTest && fakeInput && autoStage >= 5 && m != null)
        {
            LifecycleScript(m);
            return;
        }
        if (layerTest && fakeInput && autoStage >= 5)
        {
            LayerScript(Plugin.Mech);
            return;
        }
        if (!fakeInput || autoStage < 5 || m == null) return;
        if (m.Pilot == null && scriptStage < 15) return;
        if (Time.unscaledTime < scriptNext) return;
        switch (scriptStage)
        {
            case 90:
                Logger.LogInfo("PROBE_VISUAL_IDLE hands=" + (m.PilotProxy ? m.PilotProxy.HandCount : 0) + " feet=" + m.Visual.GroundedFeet + " footError=" + m.Visual.MaxFootGroundError);
                Capture("art-01-idle-right.png");
                scriptStage = 190; scriptNext = Time.unscaledTime + 0.15f;
                break;
            case 190:
                if (Environment.GetEnvironmentVariable("CU_PROBE_HANDAUDIT") == "1") StartCoroutine(AuditSeatedHand(b, m.PilotProxy));
                ProbeInput.SetKey(KeyCode.D, true);
                ProbeInput.SetMousePos(new Vector3(950, 360, 0));
                scriptStage = 91; scriptNext = Time.unscaledTime + 0.65f;
                break;
            case 91:
                Capture("art-02-walk-right.png");
                scriptStage = 191; scriptNext = Time.unscaledTime + 0.15f;
                break;
            case 191:
                ProbeInput.SetKey(KeyCode.D, false);
                ProbeInput.SetKey(KeyCode.A, true);
                ProbeInput.SetMousePos(new Vector3(320, 360, 0));
                scriptStage = 92; scriptNext = Time.unscaledTime + 0.7f;
                break;
            case 92:
                Capture("art-03-walk-left.png");
                scriptStage = 192; scriptNext = Time.unscaledTime + 0.15f;
                break;
            case 192:
                ProbeInput.SetKey(KeyCode.A, false);
                ProbeInput.SetKey(KeyCode.Space, true);
                scriptStage = 93; scriptNext = Time.unscaledTime + 0.65f;
                break;
            case 93:
                Capture("art-04-jet-left.png");
                scriptStage = 193; scriptNext = Time.unscaledTime + 0.15f;
                break;
            case 193:
                ProbeInput.SetMousePos(new Vector3(950, 360, 0));
                scriptStage = 94; scriptNext = Time.unscaledTime + 0.45f;
                break;
            case 94:
                Capture("art-05-jet-right.png");
                scriptStage = 194; scriptNext = Time.unscaledTime + 0.15f;
                break;
            case 194:
                ProbeInput.SetKey(KeyCode.Space, false);
                scriptStage = 95; scriptNext = Time.unscaledTime + 0.5f;
                break;
            case 95:
                Capture("art-06-coast.png");
                scriptStage = 96; scriptNext = Time.unscaledTime + 1.0f;
                break;
            case 96:
                Capture("art-07-descent.png");
                Logger.LogInfo("PROBE_ART_POSES_CAPTURED grounded=" + m.Grounded);
                scriptStage = 15; scriptNext = Time.unscaledTime + 0.8f;
                break;
            case 10:
                ProbeInput.SetKey(KeyCode.Space, true);
                ProbeInput.SetKey(KeyCode.D, false);
                scriptNext = Time.unscaledTime + 2.0f;
                scriptStage = 11;
                Capture("shot-script-jet.png");
                Logger.LogInfo("PROBE_SCRIPT_JET_START");
                break;
            case 11:
                ProbeInput.SetKey(KeyCode.Space, false);
                ProbeInput.SetKey(KeyCode.D, true);
                scriptNext = Time.unscaledTime + 1.2f;
                scriptStage = 12;
                Capture("shot-script-move.png");
                Logger.LogInfo("PROBE_SCRIPT_MOVE_START");
                break;
            case 12:
                ProbeInput.SetKey(KeyCode.D, false);
                ProbeInput.SetMousePos(new Vector3(980, 360, 0));
                ProbeInput.SetMouse(0, true);
                scriptNext = Time.unscaledTime + 1.2f;
                scriptStage = 13;
                Capture("shot-script-drill.png");
                Logger.LogInfo("PROBE_SCRIPT_DRILL_START");
                break;
            case 13:
                ProbeInput.SetMouse(0, false);
                ProbeInput.SetMouse(1, true);
                scriptNext = Time.unscaledTime + 1.2f;
                scriptStage = 14;
                Capture("shot-script-fire.png");
                Logger.LogInfo("PROBE_SCRIPT_FIRE_START");
                break;
            case 14:
                ProbeInput.SetMouse(1, false);
                ProbeInput.SetKey(KeyCode.F, true);
                ProbeInput.SetKey(KeyCode.F, false);
                scriptNext = Time.unscaledTime + 1.4f;
                scriptStage = 15;
                Capture("shot-script-exit.png");
                Logger.LogInfo("PROBE_SCRIPT_EXIT_REQUEST");
                break;
            case 15:
                if (m.Pilot != null)
                {
                    // If the F pulse could not find a side spot inside the test building,
                    // use the controller's forced lifecycle exit only to reach the parked
                    // assembly state. This does not fake the loadout or its persistence.
                    bool ok = m.Exit(true);
                    Logger.LogInfo("PROBE_SCRIPT_FORCED_EXIT " + ok);
                }
                ProbeInput.SetKey(KeyCode.P, true);
                ProbeInput.SetKey(KeyCode.P, false);
                scriptNext = Time.unscaledTime + 1.2f;
                scriptStage = 16;
                Capture("shot-script-assembly.png");
                Logger.LogInfo("PROBE_SCRIPT_ASSEMBLY_START pilot=" + (m.Pilot != null));
                break;
            case 16:
                Capture("shot-script-assembly-settled.png");
                scriptStage = 17;
                scriptNext = Time.unscaledTime + 0.5f;
                Logger.LogInfo("PROBE_SCRIPT_ASSEMBLY_WAIT");
                break;
            case 17:
                if (Environment.GetEnvironmentVariable("CU_PROBE_SAVEAUDIT") == "1") AuditSaveRecord(b);
                if (catalogTest) AuditCatalog();
                if (AssemblyPanel.IsOpen) ApplyTestLoadout();
                else Logger.LogInfo("PROBE_SCRIPT_ASSEMBLY_NOT_OPEN");
                scriptStage = 18;
                scriptNext = Time.unscaledTime + 1.0f;
                Capture("shot-script-loadout.png");
                Logger.LogInfo("PROBE_SCRIPT_LOADOUT_APPLIED loadout=" + (Plugin.Mech ? Plugin.Mech.Loadout.Summary : "none"));
                break;
            case 18:
                m = Plugin.Mech;
                if (m != null && m.Pilot == null) m.Enter(b);
                scriptStage = 19;
                scriptNext = Time.unscaledTime + 1.0f;
                Logger.LogInfo("PROBE_SCRIPT_REBOARD pilot=" + (m != null && m.Pilot != null));
                break;
            case 19:
                ProbeInput.SetKey(KeyCode.H, true);
                ProbeInput.SetKey(KeyCode.H, false);
                ProbeInput.SetMousePos(new Vector3(900, 330, 0));
                ProbeInput.SetMouse(0, true);
                scriptStage = 20;
                scriptNext = Time.unscaledTime + 1.5f;
                Capture("shot-script-beam.png");
                Logger.LogInfo("PROBE_SCRIPT_BEAM_HORN");
                break;
            case 20:
                ProbeInput.SetMouse(0, false);
                if (highSaveTest)
                {
                    Vector2 high = CarveHighShaft(m);
                    if (high != Vector2.zero)
                    {
                        m.transform.position = high;
                        if (m.Chassis)
                        {
                            m.Chassis.velocity = Vector2.zero;
                            // Test fixture only: hold the mech at the top of the shaft so
                            // the save is genuinely high-altitude, not after a fall.
                            m.Chassis.gravityScale = 0f;
                        }
                        scriptStage = 23;
                        scriptNext = Time.unscaledTime + 0.2f;
                        Capture("shot-highsave-high.png");
                        Logger.LogInfo("PROBE_HIGHSAVE_HIGH pos=" + high + " hull=" + m.HullSize);
                        break;
                    }
                    Logger.LogInfo("PROBE_HIGHSAVE_NO_SHAFT");
                }
                scriptStage = 21;
                scriptNext = Time.unscaledTime + 1.0f;
                Logger.LogInfo("PROBE_SCRIPT_SAVE_WAIT");
                break;
            case 21:
                try
                {
                    SaveSystem.SaveGame();
                    Logger.LogInfo("PROBE_SAVE_CALLED path=" + Application.persistentDataPath);
                }
                catch (Exception ex) { Logger.LogError("PROBE_SAVE_FAILED " + ex); }
                Capture("shot-script-save.png");
                scriptStage = 22;
                Logger.LogInfo("PROBE_SCRIPT_DONE");
                ProbeInput.ClearAll();
                break;
            case 23:
                var livePilot = PlayerCamera.main ? PlayerCamera.main.body : b;
                Logger.LogInfo("PROBE_HIGHSAVE_BEFORE pilot=" + (m != null && m.Pilot != null) +
                               " mechPos=" + (m ? (Vector2)m.transform.position : Vector2.zero) +
                               " pilotPos=" + (livePilot ? (Vector2)livePilot.transform.position : Vector2.zero) +
                               " safePoint=" + (m && m.SafePoint.HasValue ? m.SafePoint.Value.ToString() : "none"));
                if (m != null)
                {
                    bool manualExit = m.Exit(false);
                    Logger.LogInfo("PROBE_HIGH_EXIT_F ok=" + manualExit + " pilot=" + (m.Pilot != null) +
                                   " mechPos=" + m.transform.position + " reason=expect_false_until_ground");
                }
                try
                {
                    SaveSystem.SaveGame();
                    Logger.LogInfo("PROBE_HIGHSAVE_SAVE_CALLED path=" + Application.persistentDataPath);
                }
                catch (Exception ex) { Logger.LogError("PROBE_HIGHSAVE_SAVE_FAILED " + ex); }
                Capture("shot-highsave-save.png");
                scriptStage = 24;
                scriptNext = Time.unscaledTime + 1.8f;
                break;
            case 24:
                var afterPilot = PlayerCamera.main ? PlayerCamera.main.body : b;
                Logger.LogInfo("PROBE_HIGHSAVE_AFTER mech=" + (Plugin.Mech != null) +
                               " piloting=" + (Plugin.Mech != null && Plugin.Mech.Pilot != null) +
                               " playerPos=" + (afterPilot ? (Vector2)afterPilot.transform.position : Vector2.zero) +
                               " mechPos=" + (Plugin.Mech ? (Vector2)Plugin.Mech.transform.position : Vector2.zero));
                Capture("shot-highsave-after.png");
                scriptStage = 22;
                Logger.LogInfo("PROBE_SCRIPT_DONE");
                ProbeInput.ClearAll();
                break;
        }
        if (scriptStage == 22) ProbeInput.ClearAll();
    }

    private void CeilingScript(MechController m)
    {
        if (ceilingProbeObject && scriptStage >= 61 && scriptStage <= 62)
        {
            ceilingMaxY = Mathf.Max(ceilingMaxY, m.transform.position.y);
            if (m.CeilingBlocked) ceilingTrueFrames++;
            if (m.CeilingBlocked && !m.ThrustActive) ceilingThrustFrames++;
        }
        if (Time.unscaledTime < scriptNext) return;
        switch (scriptStage)
        {
            case 60:
            {
                var w = WorldGeneration.world;
                if (!w || !w.worldExists) { Logger.LogInfo("PROBE_CEILING_NO_WORLD"); scriptStage = 63; return; }
                // Fixture scenery: a real collider slab, independent of tilemap chunk update
                // timing. It tests contact/normal handling, not health or pilot state.
                ceilingProbeObject = new GameObject("ProbeCeiling");
                int ground = LayerMask.NameToLayer("Ground");
                if (ground >= 0) ceilingProbeObject.layer = ground;
                ceilingProbeObject.transform.position = m.transform.position + Vector3.up * 5f;
                var col = ceilingProbeObject.AddComponent<BoxCollider2D>();
                col.size = new Vector2(7f, 0.6f);
                var rb = ceilingProbeObject.AddComponent<Rigidbody2D>();
                rb.bodyType = RigidbodyType2D.Static;
                ceilingSlabBottom = ceilingProbeObject.transform.position.y - 0.3f;
                ceilingTrueFrames = 0;
                ceilingThrustFrames = 0;
                ceilingMaxY = m.transform.position.y;
                ProbeInput.SetKey(KeyCode.Space, true);
                scriptStage = 61;
                scriptNext = Time.unscaledTime + 2.2f;
                Capture("shot-ceiling-blocked.png");
                Logger.LogInfo("PROBE_CEILING_START colliderSlab=" + ceilingProbeObject.transform.position +
                               " mech=" + m.transform.position);
                return;
            }
            case 61:
                Logger.LogInfo("PROBE_CEILING_BLOCKED ceiling=" + m.CeilingBlocked + " reason=" + m.CeilingReason +
                               " thrust=" + m.ThrustActive + " vy=" + m.VelocityY + " y=" + m.transform.position.y +
                               " slabBottom=" + ceilingSlabBottom + " maxY=" + ceilingMaxY +
                               " trueFrames=" + ceilingTrueFrames + " blockedThrustFrames=" + ceilingThrustFrames);
                var overlaps = Physics2D.OverlapBoxAll(m.transform.position + Vector3.up * 3.2f,
                    new Vector2(7f, 3f), 0f);
                Logger.LogInfo("PROBE_CEILING_OVERLAP count=" + overlaps.Length + " names=" +
                               string.Join(",", overlaps.Select(c => c ? c.name + "@" + LayerMask.LayerToName(c.gameObject.layer) : "null")));
                var upHit = Physics2D.Raycast((Vector2)m.transform.position + Vector2.up * 3.3f, Vector2.up, 3f);
                Logger.LogInfo("PROBE_CEILING_RAY hit=" + (upHit.collider ? upHit.collider.name + " n=" + upHit.normal + " d=" + upHit.distance : "none"));
                ProbeInput.SetKey(KeyCode.D, true);
                scriptStage = 62;
                scriptNext = Time.unscaledTime + 1.5f;
                Capture("shot-ceiling-cleared.png");
                return;
            case 62:
                Logger.LogInfo("PROBE_CEILING_CLEARED ceiling=" + m.CeilingBlocked + " reason=" + m.CeilingReason +
                               " thrust=" + m.ThrustActive + " vy=" + m.VelocityY + " y=" + m.transform.position.y);
                ProbeInput.SetKey(KeyCode.Space, false);
                ProbeInput.SetKey(KeyCode.D, false);
                if (ceilingProbeObject) UnityEngine.Object.Destroy(ceilingProbeObject);
                scriptStage = 63;
                Logger.LogInfo("PROBE_CEILING_DONE");
                ProbeInput.ClearAll();
                break;
        }
    }

    private void LayerScript(MechController m)
    {
        if (Time.unscaledTime < scriptNext) return;
        switch (scriptStage)
        {
            case 80:
                var w = WorldGeneration.world;
                Logger.LogInfo("PROBE_LAYER_START depth=" + (w ? w.biomeDepth : -1) +
                               " mech=" + (m != null) + " pilot=" + (m != null && m.Pilot != null));
                if (w && m != null && m.Pilot != null)
                {
                    try { w.StartCoroutine(w.RegenerateWorld()); }
                    catch (Exception ex) { Logger.LogError("PROBE_LAYER_START_FAILED " + ex); }
                }
                Capture("shot-layer-start.png");
                scriptStage = 81;
                scriptNext = Time.unscaledTime + 7f;
                break;
            case 81:
                var w2 = WorldGeneration.world;
                var pl = PlayerCamera.main ? PlayerCamera.main.body : null;
                Logger.LogInfo("PROBE_LAYER_DONE depth=" + (w2 ? w2.biomeDepth : -1) +
                               " mech=" + (Plugin.Mech != null) +
                               " piloting=" + (Plugin.Mech != null && Plugin.Mech.Pilot != null) +
                               " playerPos=" + (pl ? (Vector2)pl.transform.position : Vector2.zero) +
                               " generating=" + (w2 && w2.generatingWorld));
                Capture("shot-layer-after.png");
                scriptStage = 82;
                ProbeInput.ClearAll();
                break;
        }
    }

    private void LifecycleScript(MechController m)
    {
        if (Time.unscaledTime < scriptNext) return;
        switch (scriptStage)
        {
            case 70:
                if (m.Pilot)
                {
                    Logger.LogInfo("PROBE_LIFECYCLE_INCAP pilot=" + (m.Pilot != null) +
                                   " consciousness=" + m.Pilot.consciousness);
                    m.Pilot.consciousness = 0f;
                }
                Capture("shot-lifecycle-incap.png");
                scriptStage = 71;
                scriptNext = Time.unscaledTime + 2.2f;
                break;
            case 71:
                var p1 = PlayerCamera.main ? PlayerCamera.main.body : null;
                Logger.LogInfo("PROBE_LIFECYCLE_EXIT pilot=" + (m != null && m.Pilot != null) +
                               " mech=" + (Plugin.Mech != null) +
                               " playerPos=" + (p1 ? (Vector2)p1.transform.position : Vector2.zero));
                if (p1) p1.consciousness = 100f;
                Capture("shot-lifecycle-exit.png");
                scriptStage = 72;
                scriptNext = Time.unscaledTime + 0.5f;
                break;
            case 72:
                var p2 = PlayerCamera.main ? PlayerCamera.main.body : null;
                Logger.LogInfo("PROBE_LIFECYCLE_REBOARD pilot=" + (m != null && m.Pilot != null) +
                               " mech=" + (Plugin.Mech != null) +
                               " playerPos=" + (p2 ? (Vector2)p2.transform.position : Vector2.zero));
                if (m != null && m.Pilot == null && p2 != null)
                {
                    m.Enter(p2);
                    Logger.LogInfo("PROBE_LIFECYCLE_REENTER ok=" + (m.Pilot != null));
                    try
                    {
                        var f = typeof(MechController).GetField("sceneDepth", BindingFlags.NonPublic | BindingFlags.Instance);
                        var w = WorldGeneration.world;
                        if (f != null && w != null) f.SetValue(m, w.biomeDepth + 1);
                    }
                    catch (Exception ex) { Logger.LogError("PROBE_LIFECYCLE_SCENE_SET_FAILED " + ex); }
                }
                Capture("shot-lifecycle-scene.png");
                scriptStage = 73;
                scriptNext = Time.unscaledTime + 1.6f;
                break;
            case 73:
                var p3 = PlayerCamera.main ? PlayerCamera.main.body : null;
                Logger.LogInfo("PROBE_LIFECYCLE_SCENE_DONE pilot=" + (Plugin.Mech != null && Plugin.Mech.Pilot != null) +
                               " mech=" + (Plugin.Mech != null) +
                               " playerPos=" + (p3 ? (Vector2)p3.transform.position : Vector2.zero));
                Capture("shot-lifecycle-scene-after.png");
                scriptStage = 74;
                ProbeInput.ClearAll();
                break;
        }
    }

    private void CombatScript(MechController m)
    {
        if (combatTarget && (scriptStage == 31 || scriptStage == 33)) AimAt(combatTarget.transform.position);
        if (friendlyTarget && scriptStage == 35) AimAt(friendlyTarget.transform.position);
        if (Time.unscaledTime < scriptNext) return;
        switch (scriptStage)
        {
            case 30:
            {
                var friendlyProbe = PlayerCamera.main ? PlayerCamera.main.body : null;
                Logger.LogInfo("PROBE_FRIENDLY_PREDICATE self=" +
                               (friendlyProbe ? MechCombat.IsFriendly(friendlyProbe, friendlyProbe) : false) +
                               " nullTarget=" + MechCombat.IsFriendly(null, friendlyProbe));
                // Spawn a native animal fixture at a controlled point. This is real target
                // damage through the game's BuildingEntity/animal path, not a fake health value.
                BuildingEntity animal = null;
                Vector3 spawnPos = m.transform.position + Vector3.right * 8f + Vector3.up * 5f;
                var spawned = Utils.Create("shadecrawler", spawnPos, 0f);
                if (spawned)
                {
                    animal = spawned.GetComponent<BuildingEntity>();
                    var spawnRb = spawned.GetComponent<Rigidbody2D>();
                    if (spawnRb) spawnRb.bodyType = RigidbodyType2D.Static;
                }
                if (animal == null)
                {
                    animal = UnityEngine.Object.FindObjectsOfType<BuildingEntity>()
                        .FirstOrDefault(x => x && x.animal && !x.cantHit && x.health > 1f);
                    if (animal != null) TeleportCombatTarget(animal, m.transform.position);
                }
                if (animal == null)
                {
                    Logger.LogInfo("PROBE_COMBAT_NO_ANIMAL");
                    scriptStage = 32;
                    scriptNext = Time.unscaledTime + 0.2f;
                    return;
                }
                combatTarget = animal;
                combatHealth = animal.health;
                AimAt(animal.transform.position);
                ProbeInput.SetMouse(1, true);
                scriptStage = 31;
                scriptNext = Time.unscaledTime + 1.8f;
                Capture("shot-combat-animal.png");
                Logger.LogInfo("PROBE_COMBAT_ANIMAL_START id=" + animal.id + " health=" + combatHealth +
                               " pos=" + animal.transform.position + " animal=" + animal.animal);
                return;
            }
            case 31:
                ProbeInput.SetMouse(1, false);
                Logger.LogInfo("PROBE_COMBAT_ANIMAL_END health=" + (combatTarget ? combatTarget.health : -1f) +
                               " delta=" + (combatTarget ? (combatHealth - combatTarget.health) : 0f));
                scriptStage = 32;
                scriptNext = Time.unscaledTime + 0.3f;
                return;
            case 32:
            {
                var building = UnityEngine.Object.FindObjectsOfType<BuildingEntity>()
                    .FirstOrDefault(x => x && !x.animal && !x.cantHit && !x.indestructible && x.health > 1f &&
                                         x != combatTarget);
                if (building == null)
                {
                    Vector3 spawnPos = m.transform.position + Vector3.right * 10f + Vector3.up * 3f;
                    var spawned = Utils.Create("leadbush", spawnPos, 0f);
                    if (spawned) building = spawned.GetComponent<BuildingEntity>();
                }
                if (building == null)
                {
                    Logger.LogInfo("PROBE_COMBAT_NO_BUILDING");
                    scriptStage = 34;
                    scriptNext = Time.unscaledTime + 0.2f;
                    return;
                }
                combatTarget = building;
                combatHealth = building.health;
                TeleportCombatTarget(building, m.transform.position);
                AimAt(building.transform.position);
                ProbeInput.SetMouse(1, true);
                scriptStage = 33;
                scriptNext = Time.unscaledTime + 1.8f;
                Capture("shot-combat-building.png");
                Logger.LogInfo("PROBE_COMBAT_BUILDING_START id=" + building.id + " health=" + combatHealth);
                return;
            }
            case 33:
                ProbeInput.SetMouse(1, false);
                Logger.LogInfo("PROBE_COMBAT_BUILDING_END health=" + (combatTarget ? combatTarget.health : -1f) +
                               " delta=" + (combatTarget ? (combatHealth - combatTarget.health) : 0f));
                scriptStage = 34;
                scriptNext = Time.unscaledTime + 0.3f;
                return;
            case 34:
            {
                var pilot = PlayerCamera.main ? PlayerCamera.main.body : null;
                var friendly = UnityEngine.Object.FindObjectsOfType<Body>()
                    .FirstOrDefault(x => x && x != pilot && x.GetComponent<TraderScript>() != null);
                if (friendly == null)
                {
                    Logger.LogInfo("PROBE_COMBAT_NO_FRIENDLY_BODY");
                    scriptStage = 36;
                    return;
                }
                friendlyTarget = friendly;
                friendlyHealth = friendly.limbs[0].skinHealth;
                TeleportCombatTarget(friendly, m.transform.position);
                AimAt(friendly.transform.position);
                ProbeInput.SetMouse(1, true);
                scriptStage = 35;
                scriptNext = Time.unscaledTime + 1.8f;
                Capture("shot-combat-friendly.png");
                Logger.LogInfo("PROBE_COMBAT_FRIENDLY_START id=" + friendly.GetInstanceID() + " skin=" + friendlyHealth);
                return;
            }
            case 35:
                ProbeInput.SetMouse(1, false);
                Logger.LogInfo("PROBE_COMBAT_FRIENDLY_END skin=" + (friendlyTarget ? friendlyTarget.limbs[0].skinHealth : -1f) +
                               " unchanged=" + (friendlyTarget && Mathf.Abs(friendlyTarget.limbs[0].skinHealth - friendlyHealth) < 0.01f));
                scriptStage = 36;
                return;
            case 36:
                Logger.LogInfo("PROBE_COMBAT_DONE");
                scriptStage = 37;
                ProbeInput.ClearAll();
                break;
        }
    }

    private void TeleportCombatTarget(Component target, Vector3 mechPos)
    {
        if (!target) return;
        Vector3 pos = mechPos + Vector3.right * 10f + Vector3.up * 3f;
        var col = target.GetComponentInChildren<Collider2D>();
        Vector3 centerOffset = col ? target.transform.TransformPoint(col.offset) - target.transform.position : Vector3.zero;
        target.transform.position = pos - centerOffset;
        var rb = target.GetComponent<Rigidbody2D>();
        if (rb && rb.bodyType != RigidbodyType2D.Static)
        {
            rb.velocity = Vector2.zero;
            rb.angularVelocity = 0f;
        }
        Logger.LogInfo("PROBE_COMBAT_TARGET_MOVED " + target.name + " pos=" + pos +
                       " centerOffset=" + centerOffset);
    }

    private void AimAt(Vector3 fallback)
    {
        if (!Camera.main) return;
        Vector3 world = fallback;
        var target = combatTarget;
        Collider2D col = null;
        if (target)
        {
            col = target.GetComponentInChildren<Collider2D>();
            if (col) world = target.transform.TransformPoint(col.offset);
        }
        else if (friendlyTarget)
        {
            col = friendlyTarget.GetComponentInChildren<Collider2D>();
            if (col) world = friendlyTarget.transform.TransformPoint(col.offset);
        }
        Vector3 screen = Camera.main.WorldToScreenPoint(world);
        ProbeInput.SetMousePos(new Vector3(screen.x, screen.y, 0f));
    }

    private void Capture(string name)
    {
        try { ScreenCapture.CaptureScreenshot(Path.Combine(output, name), 1); }
        catch (Exception ex) { Logger.LogWarning("PROBE_CAPTURE_FAILED " + ex.Message); }
    }

    private void AuditCatalog()
    {
        int checkedKeys = 0, missing = 0;
        var miss = new System.Collections.Generic.List<string>();
        void Check(string key)
        {
            if (string.IsNullOrEmpty(key)) return;
            checkedKeys++;
            if (MechArt.Sprite(key) == null)
            {
                missing++;
                if (miss.Count < 24) miss.Add(key);
            }
        }
        int bodies = 0, arms = 0, legs = 0, boosters = 0, horns = 0;
        foreach (var p in MechCatalog.Everything)
        {
            if (p.Slot == MechSlot.Body)
            {
                bodies++;
                Check("body_" + p.Id + "_back_bone");
                Check("body_" + p.Id + "_front_bone");
            }
            else if (p.Slot == MechSlot.ArmLeft)
            {
                arms++;
                Check(p.ArtPrefix + "_bone");
                if (p.Behavior == ArmBehavior.Drill) { Check("arm_drill_bit0_bone"); Check("arm_drill_bit1_bone"); }
                else if (p.Behavior == ArmBehavior.Cannon) { Check("arm_cannon_flash0_bone"); Check("arm_cannon_flash1_bone"); }
                else if (p.Behavior == ArmBehavior.BeamDrill) Check("arm_beam_glow_bone");
            }
            else if (p.Slot == MechSlot.Legs)
            {
                legs++;
                Check(p.ArtPrefix + "_thigh_bone");
                Check(p.ArtPrefix + "_shin_bone");
                Check(p.ArtPrefix + "_foot_bone");
            }
            else if (p.Slot == MechSlot.Booster)
            {
                boosters++;
                Check(p.ArtPrefix + "_bone");
                Check(p.ArtPrefix + "_flame0_bone");
                Check(p.ArtPrefix + "_flame1_bone");
            }
            else if (p.Slot == MechSlot.Horn && p.Id != "none")
            {
                horns++;
                Check(p.ArtPrefix + "_bone");
            }
            Check(p.IconKey);
        }
        Logger.LogInfo("PROBE_CATALOG_AUDIT bodies=" + bodies + " arms=" + arms + " legs=" + legs +
                       " boosters=" + boosters + " horns=" + horns + " keys=" + checkedKeys +
                       " missing=" + missing + " sample=" + string.Join(",", miss));

        if (Plugin.Mech && !Plugin.Mech.Pilot)
        {
            int built = 0, failed = 0;
            var firstFailure = "";
            var preview = PlayerCamera.main ? PlayerCamera.main.body : null;
            foreach (var p in MechCatalog.Everything)
            {
                var l = MechLoadout.Default();
                if (p.Slot == MechSlot.ArmLeft) { l.LeftArmId = p.Id; l.RightArmId = MechCatalog.DefaultRightArm; }
                else if (p.Slot == MechSlot.ArmRight) { l.RightArmId = p.Id; l.LeftArmId = MechCatalog.DefaultLeftArm; }
                else l.Set(p.Slot, p.Id);
                if (Plugin.Mech.ApplyPreviewLoadout(l, preview, out string reason))
                    built++;
                else
                {
                    failed++;
                    if (firstFailure.Length == 0) firstFailure = p.Slot + ":" + p.Id + "=" + reason;
                }
            }
            Logger.LogInfo("PROBE_CATALOG_BUILD built=" + built + " failed=" + failed +
                           " firstFailure=" + firstFailure);
        }
    }

    private void ApplyTestLoadout()
    {
        try
        {
            var panel = Plugin.Instance ? Plugin.Instance.GetComponent<AssemblyPanel>() : null;
            if (!panel) { Logger.LogInfo("PROBE_LOADOUT_NO_PANEL"); return; }
            var t = typeof(AssemblyPanel);
            var slotField = t.GetField("selectedSlot", BindingFlags.NonPublic | BindingFlags.Instance);
            var selectPalette = t.GetMethod("SelectPalette", BindingFlags.NonPublic | BindingFlags.Instance);
            var selectPart = t.GetMethod("SelectPart", BindingFlags.NonPublic | BindingFlags.Instance);
            var apply = t.GetMethod("Apply", BindingFlags.NonPublic | BindingFlags.Instance);
            var parts = catalogTest
                ? new (MechSlot slot, MechPartDef part)[]
                {
                    (MechSlot.Body, MechCatalog.Get(MechSlot.Body, "sb_apex")),
                    (MechSlot.Legs, MechCatalog.Get(MechSlot.Legs, "sb_arc")),
                    (MechSlot.Booster, MechCatalog.Get(MechSlot.Booster, "sb_zed")),
                    (MechSlot.ArmLeft, MechCatalog.GetArm("sb_soluskatana")),
                    (MechSlot.ArmRight, MechCatalog.GetArm("sb_gatling")),
                    (MechSlot.Horn, MechCatalog.Get(MechSlot.Horn, "sb_train")),
                }
                : new (MechSlot slot, MechPartDef part)[]
                {
                    (MechSlot.Body, MechCatalog.Get(MechSlot.Body, "venturer_open")),
                    (MechSlot.Legs, MechCatalog.Get(MechSlot.Legs, "hopper")),
                    (MechSlot.Booster, MechCatalog.Get(MechSlot.Booster, "twinjet")),
                    (MechSlot.ArmLeft, MechCatalog.GetArm("beamdrill")),
                    (MechSlot.ArmRight, MechCatalog.GetArm("drill")),
                    (MechSlot.Horn, MechCatalog.Get(MechSlot.Horn, "siren")),
                };
            foreach (var (slot, part) in parts)
            {
                if (part == null) { Logger.LogInfo("PROBE_LOADOUT_MISSING " + slot); continue; }
                slotField.SetValue(panel, slot);
                selectPart.Invoke(panel, new object[] { part });
            }
            selectPalette.Invoke(panel, new object[] { catalogTest ? "steel" : "olive" });
            apply.Invoke(panel, null);
            Logger.LogInfo("PROBE_LOADOUT_APPLIED store=" + MechLoadoutStore.Get(PlayerCamera.main.body).Summary +
                           " palette=" + MechLoadoutStore.Get(PlayerCamera.main.body).PaletteId);
        }
        catch (Exception ex) { Logger.LogError("PROBE_LOADOUT_FAILED " + ex); }
    }

    public void Tick()
    {
        if (!string.IsNullOrEmpty(output)) ApplyFps();
        float now = Time.unscaledTime;
        if (windowStart <= 0f) windowStart = now;
        frameCount++;
        float dt = Time.unscaledDeltaTime;
        if (frameCount - 1 < frameTimes.Length) frameTimes[frameCount - 1] = dt * 1000f;

        var b = PlayerCamera.main ? PlayerCamera.main.body : null;
        var m = Plugin.Mech;
        if (m != trackedMech)
        {
            trackedMech = m;
            windowBrokenStart = m ? m.BrokenBlocks : 0;
            wasPiloting = false;
        }
        if (b)
        {
            int py = Mathf.RoundToInt(b.transform.position.y);
            maxPlayerY = Math.Max(maxPlayerY, py); minPlayerY = Math.Min(minPlayerY, py);
        }
        if (m)
        {
            float my = m.transform.position.y;
            maxMechY = Math.Max(maxMechY, (int)Mathf.Round(my)); minMechY = Math.Min(minMechY, (int)Mathf.Round(my));
            maxVy = Math.Max(maxVy, m.VelocityY); minVy = Math.Min(minVy, m.VelocityY);
            if (m.ThrustActive) windowThrustFrames++;
            windowSamples++;
            if (m.Pilot != null)
            {
                if (!wasPiloting)
                {
                    wasPiloting = true;
                    pilotStartY = my; pilotMaxY = my; pilotMinY = my;
                    pilotStartTime = now;
                    pilotMaxVy = m.VelocityY; pilotMinVy = m.VelocityY;
                    pilotStartBroken = m.BrokenBlocks; pilotThrustFrames = 0;
                }
                pilotMaxY = Math.Max(pilotMaxY, my); pilotMinY = Math.Min(pilotMinY, my);
                pilotMaxVy = Math.Max(pilotMaxVy, m.VelocityY); pilotMinVy = Math.Min(pilotMinVy, m.VelocityY);
                if (m.ThrustActive) pilotThrustFrames++;
            }
        }
        if (m == null || m.Pilot == null)
        {
            if (wasPiloting) { wasPiloting = false; PilotSessionEnd(now); }
        }
        TryAutoStart(now);
        AutoPlay(b, m);
        HandleFixtures(b, m);
        if (now - windowStart < 0.5f) return;
        FlushWindow(now, b, m);
    }

    private void TryAutoStart(float now)
    {
        if (!autoStart || autoStartTried) return;
        var pre = PreRunScript.instance;
        if (pre == null) return;
        if (WorldGeneration.world && WorldGeneration.world.worldExists) { autoStartTried = true; return; }
        if (titleSince <= 0f) titleSince = now;
        if (now - titleSince < 2.0f) return;
        autoStartTried = true;
        try
        {
            // Test-only environment setup: the title UI can be invisible in the isolated
            // sandbox, so enter a normal new run through the game's own StartRun path.
            // Gameplay still uses real keyboard/mouse input after the world is ready.
            PlayerPrefs.SetInt("didbasiccourse", 1);
            if (pre.contentWarning) pre.contentWarning.SetActive(false);
            if (pre.tutorialWarning) pre.tutorialWarning.SetActive(false);
            if (pre.runSettingsScreen) pre.runSettingsScreen.SetActive(false);
            if (loadSave)
            {
                pre.LoadRun();
                Logger.LogInfo("PROBE_AUTOLOAD_CALLED");
            }
            else
            {
                pre.StartRun();
                Logger.LogInfo("PROBE_AUTOSTART_CALLED");
            }
        }
        catch (Exception ex)
        {
            Logger.LogError("PROBE_AUTOSTART_FAILED " + ex);
        }
    }

    private void HandleFixtures(Body b, MechController m)
    {
        if (b == null) return;
        if (Input.GetKeyDown(KeyCode.F9))
        {
            // Fixture: restore the beacon after an accidental drop in a test run.
            if (!Plugin.HasBeacon(b))
            {
                var obj = Utils.Create(Plugin.BeaconId, b.transform.position, 0f);
                if (obj && obj.TryGetComponent<Item>(out var item))
                {
                    var slot = b.FirstEmptySlot();
                    if (slot.HasValue) b.PickUpItem(item, slot.Value, true);
                    else obj.transform.position = b.transform.position;
                    Logger.LogInfo("FIXTURE_BEACON_RESTORED");
                }
            }
            else Logger.LogInfo("FIXTURE_BEACON_ALREADY_PRESENT");
        }
        if (m == null) return;
        if (Input.GetKeyDown(KeyCode.F7))
        {
            if (m != null)
            {
                // Fixture: put the player at a safe side spot for a real F board.
                if (m.Pilot != null) { Logger.LogInfo("FIXTURE_F7_ALREADY_PILOTING"); return; }
                Vector2 size = new Vector2(2.2f, 4.6f);
                Vector2 pos;
                if (!MechSafeExit.FindNearby(m.transform.position, size, out pos))
                    pos = (Vector2)m.transform.position + Vector2.up * 4.6f;
                TeleportPlayer(b, pos);
                Logger.LogInfo("FIXTURE_F7_PLAYER_TO_MECH " + pos);
            }
            else
            {
                // Fixture: put the player on the floor of a wide chamber, then summon normally.
                Vector2 slot;
                if (!TryFindWideChamber(out slot)) { Logger.LogInfo("FIXTURE_F7_NO_CHAMBER"); return; }
                Vector2 playerPos = slot + new Vector2(-3f, -0.9f);
                TeleportPlayer(b, playerPos);
                Logger.LogInfo("FIXTURE_F7_PLAYER_CHAMBER player=" + playerPos + " mechSlot=" + slot);
            }
        }
        if (Input.GetKeyDown(KeyCode.F8) && m != null)
        {
            // Fixture: place the mech and player in a large open shaft for real flight tests.
            if (m.Pilot != null) { Logger.LogInfo("FIXTURE_F8_ALREADY_PILOTING"); return; }
            Vector2 mechPos, playerPos;
            if (!TryFindShaft(out mechPos, out playerPos)) { Logger.LogInfo("FIXTURE_F8_NO_SHAFT"); return; }
            m.transform.position = mechPos;
            if (m.Chassis) m.Chassis.velocity = Vector2.zero;
            TeleportPlayer(b, mechPos + Vector2.up * 4.4f);
            Logger.LogInfo("FIXTURE_F8_ARENA mech=" + mechPos + " player=" + (Vector2)b.transform.position);
        }
        if (Input.GetKeyDown(KeyCode.F1) && m != null)
        {
            // Fixture: move to a wide chamber and put a flat 11-cell steel ceiling above it,
            // so the ceiling-contact test is not confounded by narrow natural side walls.
            var w = WorldGeneration.world;
            if (!w || !w.worldExists) { Logger.LogInfo("FIXTURE_CEILING_NO_WORLD"); return; }
            Vector2 slot;
            if (TryFindWideChamber(out slot))
            {
                m.transform.position = slot;
                if (m.Chassis) m.Chassis.velocity = Vector2.zero;
                if (PlayerCamera.main) PlayerCamera.main.transform.position = slot;
            }
            else
            {
                slot = m.transform.position;
                Logger.LogInfo("FIXTURE_CEILING_NO_CHAMBER using current position");
            }
            Vector2Int baseCell = w.WorldToBlockPos(slot);
            int placed = 0;
            // Narrow slab over the left half: moving right clears it without reaching the
            // chamber wall, proving the "leave the ceiling, resume thrust" path.
            for (int dx = -2; dx <= 0; dx++)
            {
                var cell = new Vector2Int(baseCell.x + dx, baseCell.y + 9);
                if (cell.x < 1 || cell.y < 1 || cell.x >= (int)w.width - 1 || cell.y >= (int)w.height - 1) continue;
                w.SetBlock(cell, 6);
                placed++;
            }
            Logger.LogInfo("FIXTURE_CEILING id=6 placed=" + placed + " baseCell=" + baseCell + " mech=" + slot);
        }
        if (Input.GetKeyDown(KeyCode.F10) && m != null) BuildMaterialWall(6, m);
        if (Input.GetKeyDown(KeyCode.F11) && m != null) BuildMaterialWall(10, m);
        if (Input.GetKeyDown(KeyCode.F12) && m != null) BuildMaterialWall(1, m);
    }

    private void BuildMaterialWall(ushort id, MechController m)
    {
        var w = WorldGeneration.world;
        if (!w || !w.worldExists) { Logger.LogInfo("FIXTURE_WALL_NO_WORLD"); return; }
        Vector2 center = m != null ? (Vector2)m.transform.position : (Vector2)PlayerCamera.main.body.transform.position;
        Vector2Int baseCell = w.WorldToBlockPos(center);
        int placed = 0;
        for (int dx = 6; dx <= 18; dx++)
        for (int dy = -5; dy <= 4; dy++)
        {
            int x = baseCell.x + dx, y = baseCell.y + dy;
            if (x < 1 || y < 1 || x >= (int)w.width - 1 || y >= (int)w.height - 1) continue;
            w.SetBlock(new Vector2Int(x, y), id);
            placed++;
        }
        var info = w.GetBlockInfo(id);
        Logger.LogInfo("FIXTURE_WALL id=" + id + " name=" + (info != null ? info.name : "?") +
                       " hp=" + (info != null ? info.health : 0f) + " placed=" + placed +
                       " baseCell=" + baseCell + " center=" + center);
    }

    private void TeleportPlayer(Body b, Vector2 pos)
    {
        b.transform.position = pos;
        if (b.rb) { b.rb.velocity = Vector2.zero; b.rb.angularVelocity = 0f; }
        if (PlayerCamera.main) PlayerCamera.main.transform.position = pos;
    }

    /// <summary>
    /// Test-only scenery fixture: carve a flat, open arena in the isolated sandbox so
    /// summon/flight/ceiling tests do not depend on a random world seed. It never sets
    /// piloting, health, or fake combat state; it only prepares terrain and moves the
    /// player to that terrain.
    /// </summary>
    /// <summary>
    /// Test fixture only: carve a tall, narrow shaft above the mech and place it high
    /// in the air. No side landing or adjacent floor is left, so a successful save
    /// can only come from the controller's downward terrain scan.
    /// </summary>
    private Vector2 CarveHighShaft(MechController mech)
    {
        var w = WorldGeneration.world;
        if (!w || !w.worldExists || !mech) return Vector2.zero;
        Vector2Int cell = w.WorldToBlockPos(mech.transform.position);
        int top = Mathf.Min((int)w.height - 8, cell.y + 54);
        for (int dx = -10; dx <= 10; dx++)
        {
            int x = cell.x + dx;
            if (x < 2 || x >= (int)w.width - 2) continue;
            for (int y = cell.y + 2; y <= top; y++)
                if (w.GetBlock(new Vector2Int(x, y)) != 0) w.SetBlock(new Vector2Int(x, y), 0);
            w.SetBlock(new Vector2Int(x, cell.y - 2), 6);
        }
        Vector2 floor = w.BlockToWorldPos(new Vector2Int(cell.x, cell.y - 2));
        Vector2 high = new Vector2(floor.x, floor.y + 0.5f + mech.HullSize.y * 0.5f + 25f);
        Logger.LogInfo("PROBE_HIGH_SHAFT_CARVED x=" + cell.x + " floorY=" + (cell.y - 2) +
                       " top=" + top + " high=" + high);
        return high;
    }

    private bool TryBuildArena(Body b, out Vector2 playerPos)
    {
        playerPos = Vector2.zero;
        var w = WorldGeneration.world;
        if (!w || !w.worldExists) return false;
        Vector2Int origin = w.WorldToBlockPos(b.transform.position);
        int[] offsets = { 96, -96, 128, -128, 160, -160, 64, -64, 48, -48 };
        foreach (int off in offsets)
        {
            int cx = origin.x + off;
            int floorY = origin.y + 30;
            if (floorY >= (int)w.height - 30) floorY = origin.y - 30;
            if (cx < 26 || cx >= (int)w.width - 26 || floorY < 26 || floorY >= (int)w.height - 30) continue;
            int half = 19, height = 24, cleared = 0, floor = 0;
            for (int dx = -half; dx <= half; dx++)
            {
                int x = cx + dx;
                if (x < 3 || x >= (int)w.width - 3) continue;
                for (int dy = 1; dy <= height; dy++)
                {
                    int y = floorY + dy;
                    if (y < 2 || y >= (int)w.height - 2) continue;
                    if (w.GetBlock(new Vector2Int(x, y)) != 0) { w.SetBlock(new Vector2Int(x, y), 0); cleared++; }
                }
                w.SetBlock(new Vector2Int(x, floorY), 6);
                floor++;
            }
            Vector2 floorWorld = w.BlockToWorldPos(new Vector2Int(cx, floorY));
            playerPos = new Vector2(floorWorld.x - 4f, floorWorld.y + 0.5f + 2.2f);
            int removed = 0;
            foreach (var bld in UnityEngine.Object.FindObjectsOfType<BuildingEntity>())
            {
                if (bld && Vector2.Distance(bld.transform.position, floorWorld) < 35f)
                {
                    UnityEngine.Object.Destroy(bld.gameObject);
                    removed++;
                }
            }
            Logger.LogInfo("PROBE_ARENA cell=" + cx + "," + floorY + " offset=" + off +
                           " cleared=" + cleared + " floor=" + floor + " removedBuildings=" + removed +
                           " player=" + playerPos);
            return true;
        }
        Logger.LogInfo("PROBE_ARENA_NO_CANDIDATE");
        return false;
    }

    private bool BuildingNear(Vector2 pos, float radius)
    {
        foreach (var bld in UnityEngine.Object.FindObjectsOfType<BuildingEntity>())
        {
            if (bld && Vector2.Distance(pos, bld.transform.position) < radius) return true;
        }
        return false;
    }

    private bool TryFindShaft(out Vector2 mechPos, out Vector2 playerPos)
    {
        mechPos = Vector2.zero;
        playerPos = Vector2.zero;
        var w = WorldGeneration.world;
        if (!w || !w.worldExists) return false;
        Vector2Int origin = w.WorldToBlockPos(PlayerCamera.main.body.transform.position);
        int width = (int)w.width, height = (int)w.height;
        int bestX = -1, bestFloor = -1, bestLen = 0, bestScore = int.MinValue;
        int x0 = 4;
        int x1 = width - 5;
        for (int x = x0; x <= x1; x++)
        {
            int runStart = -1;
            for (int y = 3; y < height - 3; y++)
            {
                bool air = w.GetBlock(new Vector2Int(x, y)) == 0;
                if (air)
                {
                    if (runStart < 0) runStart = y;
                }
                else
                {
                    if (runStart >= 0)
                    {
                        int len = y - runStart;
                        if (len >= 70 && IsShaftClear(w, x, runStart, y - 1))
                        {
                            int score = len - Mathf.Abs(x - origin.x) / 4;
                            if (score > bestScore) { bestScore = score; bestX = x; bestFloor = runStart - 1; bestLen = len; }
                        }
                        runStart = -1;
                    }
                }
            }
        }
        if (bestX < 0)
        {
            // Fall back to a wide horizontal arena; flight height may be limited by its ceiling.
            for (int x = x0; x <= x1; x += 4)
            for (int y = 3; y < height - 12; y++)
            {
                if (w.GetBlock(new Vector2Int(x, y - 1)) == 0 || w.GetBlock(new Vector2Int(x, y)) != 0) continue;
                if (!IsShaftClear(w, x, y, y + 8)) continue;
                bestX = x; bestFloor = y - 1; bestLen = 9; break;
            }
            if (bestX < 0) return false;
        }
        Vector2 floor = w.BlockToWorldPos(new Vector2Int(bestX, bestFloor));
        mechPos = new Vector2(floor.x, floor.y + 0.5f + MechController.Size.y * 0.5f + 0.2f);
        playerPos = new Vector2(floor.x, floor.y + 0.5f + 2.55f);
        Logger.LogInfo("FIXTURE_SHAFT x=" + bestX + " floor=" + bestFloor + " len=" + bestLen + " mechSlot=" + mechPos + " playerSlot=" + playerPos);
        return true;
    }

    private static bool IsShaftClear(WorldGeneration w, int x, int y0, int y1)
    {
        if (y1 - y0 < 7) return false;
        int top = Mathf.Min(y1, y0 + 70);
        for (int dx = -2; dx <= 2; dx++)
        {
            int xx = x + dx;
            if (xx < 1 || xx >= (int)w.width - 1) return false;
            if (w.GetBlock(new Vector2Int(xx, y0 - 1)) == 0) return false;
            for (int y = y0; y <= top; y++)
                if (w.GetBlock(new Vector2Int(xx, y)) != 0) return false;
        }
        return true;
    }

    private bool TryFindWideChamber(out Vector2 mechPos)
    {
        mechPos = Vector2.zero;
        var w = WorldGeneration.world;
        if (!w || !w.worldExists) return false;
        Vector2Int origin = w.WorldToBlockPos(PlayerCamera.main.body.transform.position);
        int width = (int)w.width, height = (int)w.height;
        int bestX = -1, bestFloor = -1, bestScore = int.MinValue;
        for (int x = 6; x < width - 6; x++)
        for (int y = 4; y < height - 36; y++)
        {
            if (w.GetBlock(new Vector2Int(x, y - 1)) == 0) continue;
            bool clear = true;
            for (int dx = -6; dx <= 6 && clear; dx++)
            for (int dy = 0; dy <= 24; dy++)
            {
                if (w.GetBlock(new Vector2Int(x + dx, y + dy)) != 0) { clear = false; break; }
            }
            if (!clear) continue;
            // The floor must be solid under the whole width.
            for (int dx = -6; dx <= 6; dx++)
                if (w.GetBlock(new Vector2Int(x + dx, y - 1)) == 0) { clear = false; break; }
            if (!clear) continue;
            int score = 1000 - Mathf.Abs(x - origin.x) - Mathf.Abs(y - origin.y);
            if (score > bestScore) { bestScore = score; bestX = x; bestFloor = y - 1; }
        }
        if (bestX < 0) return false;
        Vector2 floor = w.BlockToWorldPos(new Vector2Int(bestX, bestFloor));
        mechPos = new Vector2(floor.x, floor.y + 0.5f + MechController.Size.y * 0.5f + 0.2f);
        Logger.LogInfo("FIXTURE_WIDE_CHAMBER x=" + bestX + " floor=" + bestFloor + " mechSlot=" + mechPos);
        return true;
    }

    private void ApplyFps()
    {
        if (fpsApplied) return;
        if (targetFps > 0)
        {
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = targetFps;
            Logger.LogInfo("PROBE_TARGET_FPS " + targetFps + " vSync=0");
        }
        fpsApplied = true;
    }

    private void FlushWindow(float now, Body b, MechController m)
    {
        try
        {
            float[] slice = frameTimes.Take(Math.Min(frameCount, frameTimes.Length)).OrderBy(x => x).ToArray();
            float p50 = Percentile(slice, 0.50f);
            float p95 = Percentile(slice, 0.95f);
            float avg = slice.Length > 0 ? slice.Average() : 0f;
            var record = new Dictionary<string, object>
            {
                ["utc"] = DateTime.UtcNow.ToString("o"),
                ["windowSeconds"] = now - windowStart,
                ["frames"] = frameCount,
                ["frameMsAvg"] = Round(avg),
                ["frameMsP50"] = Round(p50),
                ["frameMsP95"] = Round(p95),
                ["ready"] = Plugin.Ready,
                ["inputReady"] = Plugin.InputReady(),
                ["paused"] = PauseHandler.paused,
                ["focused"] = Application.isFocused,
                ["timeScale"] = Time.timeScale,
                ["loadedRun"] = SaveSystem.loadedRun,
                ["unityDefaultDataPath"] = Application.persistentDataPath,
                ["isolatedDataPath"] = Environment.GetEnvironmentVariable("CU_TEST_DATA"),
                ["storedLoadout"] = b ? MechLoadoutStore.Get(b).Summary : "",
                ["storedPalette"] = b ? MechLoadoutStore.Get(b).PaletteId : ""
            };
            if (b)
            {
                record["player"] = new
                {
                    x = Round(b.transform.position.x), y = Round(b.transform.position.y),
                    conscious = b.conscious,
                    beaconCount = b.GetComponentsInChildren<Item>(true).Count(i => i.id == Plugin.BeaconId),
                    simulated = b.rb.simulated, forceNoMovement = b.forceNoMovement
                };
            }
            var w = WorldGeneration.world;
            if (w && w.worldExists && Camera.main)
            {
                Vector2 cp = Camera.main.ScreenToWorldPoint(Input.mousePosition);
                int cid = w.GetBlock(cp);
                var cinfo = cid != 0 ? w.GetBlockInfo((ushort)cid) : null;
                record["cursorBlock"] = new
                {
                    x = Round(cp.x), y = Round(cp.y), id = cid,
                    name = cinfo != null ? cinfo.name : "",
                    health = cinfo != null ? Round(cinfo.health) : 0f
                };
            }
            if (m && !m.Grounded && m.ThrustActive && Mathf.Abs(m.VelocityY) < 4f)
            {
                Vector2 above = (Vector2)m.transform.position + Vector2.up * (MechController.Size.y * 0.5f);
                var cols = Physics2D.OverlapBoxAll(above, new Vector2(MechController.Size.x, 0.6f), 0f);
                var list = new List<object>();
                foreach (var c in cols)
                {
                    if (!c || c.gameObject == m.gameObject || c.transform.IsChildOf(m.transform)) continue;
                    list.Add(new
                    {
                        name = c.name,
                        layer = LayerMask.LayerToName(c.gameObject.layer),
                        type = c.GetType().Name,
                        trigger = c.isTrigger
                    });
                }
                record["ceilingProbe"] = list;
            }
            if (m)
            {
                record["mech"] = new
                {
                    x = Round(m.transform.position.x), y = Round(m.transform.position.y),
                    piloting = m.Pilot != null,
                    grounded = m.Grounded, thrustActive = m.ThrustActive, ceilingBlocked = m.CeilingBlocked,
                    vx = Round(m.VelocityX), vy = Round(m.VelocityY),
                    holdTime = Round(m.HoldTime), facing = m.Visual ? m.Visual.Facing : 0,
                    drillHeld = m.DrillHeld, drillContact = m.DrillContact,
                    drillHits = m.DrillHits, shots = m.Shots, brokenBlocks = m.BrokenBlocks,
                    drillPulses = m.DrillPulses,
                    drillCandidates = m.LastPulseCandidates, drillProcessed = m.LastPulseProcessed,
                    lastPulseBroken = m.LastPulseBroken, lastDrillMs = Round(m.LastDrillMs),
                    avgDrillMs = Round(m.AverageDrillMs),
                    blockId = m.LastBlockId, blockName = m.LastBlockName, blockHealth = Round(m.LastBlockHealth),
                    breakId = m.LastBreakId, breakHealth = Round(m.LastBreakHealth), breakMs = Round(m.LastBreakMs),
                    safePoint = m.SafePoint.HasValue
                        ? new { x = Round(m.SafePoint.Value.x), y = Round(m.SafePoint.Value.y) }
                        : null,
                    loadout = m.Loadout != null ? m.Loadout.Summary : "",
                    palette = m.Loadout != null ? m.Loadout.PaletteId : "",
                    leftArm = m.LeftArmId, rightArm = m.RightArmId,
                    hull = new { x = Round(m.HullSize.x), y = Round(m.HullSize.y) },
                    ceilingReason = m.CeilingReason,
                    lastHitKind = m.LastHitKind, lastHitTarget = m.LastHitTarget, lastHitDamage = Round(m.LastHitDamage),
                    groundedFeet = m.Visual != null ? m.Visual.GroundedFeet : 0,
                    footGroundError = m.Visual != null ? m.Visual.MaxFootGroundError : 0f,
                    seatedHands = m.PilotProxy != null ? m.PilotProxy.HandCount : 0,
                    pilotProxy = m.PilotProxy != null && m.PilotProxy.HasFace,
                    proxyHead = m.PilotProxy != null ? m.PilotProxy.HeadSprite : "none",
                    proxyEyes = m.PilotProxy != null ? m.PilotProxy.EyesSprite : "none",
                    proxyHeadMirrors = m.PilotProxy != null ? m.PilotProxy.HeadMirrorCount : 0,
                    proxyTorsoMirrors = m.PilotProxy != null ? m.PilotProxy.TorsoMirrorCount : 0
                };
                record["windowMech"] = new
                {
                    maxY = maxMechY > int.MinValue ? maxMechY : 0,
                    minY = minMechY < int.MaxValue ? minMechY : 0,
                    maxVy = Round(maxVy > float.MinValue ? maxVy : 0),
                    minVy = Round(minVy < float.MaxValue ? minVy : 0),
                    thrustFrames = windowThrustFrames,
                    brokenDelta = m.BrokenBlocks - windowBrokenStart,
                    samples = windowSamples
                };
                if (wasPiloting)
                {
                    record["pilotSession"] = new
                    {
                        startY = Round(pilotStartY),
                        maxY = Round(pilotMaxY),
                        minY = Round(pilotMinY),
                        rise = Round(pilotMaxY - pilotStartY),
                        maxVy = Round(pilotMaxVy),
                        minVy = Round(pilotMinVy),
                        thrustFrames = pilotThrustFrames,
                        broken = m.BrokenBlocks - pilotStartBroken,
                        seconds = Round(now - pilotStartTime)
                    };
                }
                if (Time.unscaledTime >= nextScan)
                {
                    nextScan = Time.unscaledTime + 5f;
                    record["nearbyBlocks"] = ScanBlocks(m.transform.position);
                }
            }
            var json = JsonConvert.SerializeObject(record);
            File.WriteAllText(Path.Combine(output, "status.json"), json);
            File.AppendAllText(Path.Combine(output, "observations.jsonl"), json + "\n");
        }
        catch (Exception ex) { Logger.LogWarning(ex.Message); }
        finally
        {
            windowStart = now;
            frameCount = 0;
            maxMechY = int.MinValue; minMechY = int.MaxValue;
            maxPlayerY = int.MinValue; minPlayerY = int.MaxValue;
            maxVy = float.MinValue; minVy = float.MaxValue;
            windowThrustFrames = 0; windowSamples = 0;
            if (m) windowBrokenStart = m.BrokenBlocks;
        }
    }

    private void PilotSessionEnd(float now)
    {
        try
        {
            var record = new Dictionary<string, object>
            {
                ["utc"] = DateTime.UtcNow.ToString("o"),
                ["event"] = "PILOT_SESSION_END",
                ["startY"] = Round(pilotStartY),
                ["maxY"] = Round(pilotMaxY),
                ["minY"] = Round(pilotMinY),
                ["rise"] = Round(pilotMaxY - pilotStartY),
                ["maxVy"] = Round(pilotMaxVy),
                ["minVy"] = Round(pilotMinVy),
                ["thrustFrames"] = pilotThrustFrames,
                ["seconds"] = Round(now - pilotStartTime)
            };
            File.AppendAllText(Path.Combine(output, "observations.jsonl"), JsonConvert.SerializeObject(record) + "\n");
        }
        catch { }
    }

    private object ScanBlocks(Vector2 center)
    {
        var w = WorldGeneration.world;
        if (!w || !w.worldExists) return null;
        Vector2Int c = w.WorldToBlockPos(center);
        var counts = new Dictionary<int, int>();
        var nearest = new Dictionary<int, object>();
        for (int dx = -24; dx <= 24; dx++)
        for (int dy = -18; dy <= 18; dy++)
        {
            int x = c.x + dx, y = c.y + dy;
            if (x < 1 || y < 1 || x >= (int)w.width - 1 || y >= (int)w.height - 1) continue;
            int id = w.GetBlock(new Vector2Int(x, y));
            if (id == 0) continue;
            if (id == 5 || id == 6 || id == 10 || id == 14 || id == 42)
            {
                counts.TryGetValue(id, out int n);
                counts[id] = n + 1;
                if (!nearest.ContainsKey(id))
                    nearest[id] = new { x, y, d = Round(Vector2.Distance(center, w.BlockToWorldPos(new Vector2Int(x, y)))) };
            }
        }
        return new { radius = "24x18 cells", counts, nearest };
    }

    private static float Percentile(float[] sorted, float p)
    {
        if (sorted.Length == 0) return 0f;
        int idx = Mathf.Clamp(Mathf.RoundToInt((sorted.Length - 1) * p), 0, sorted.Length - 1);
        return sorted[idx];
    }

    private static float Round(float v) => (float)Math.Round(v, 3);
}

// Test-only synthetic input bridge. It is enabled only by CU_PROBE_FAKEINPUT=1 and is
// used when the Windows desktop is locked so SendInput cannot reach the game. Real
// keyboard/mouse runs remain the acceptance path when the desktop is unlocked.
public static class ProbeInput
{
    public static bool Enabled;
    public static readonly HashSet<KeyCode> Held = new HashSet<KeyCode>();
    public static readonly HashSet<KeyCode> Down = new HashSet<KeyCode>();
    public static readonly HashSet<KeyCode> Consumed = new HashSet<KeyCode>();
    public static readonly HashSet<int> MouseHeld = new HashSet<int>();
    public static readonly HashSet<int> MouseDown = new HashSet<int>();
    public static bool MouseOverride;
    public static Vector3 MousePos;

    public static void SetKey(KeyCode key, bool held)
    {
        if (held)
        {
            if (!Held.Contains(key))
            {
                Down.Add(key);
                Consumed.Remove(key);
            }
            Held.Add(key);
        }
        else
        {
            Held.Remove(key);
        }
    }

    public static void SetMouse(int button, bool held)
    {
        if (held)
        {
            if (!MouseHeld.Contains(button)) MouseDown.Add(button);
            MouseHeld.Add(button);
        }
        else
        {
            MouseHeld.Remove(button);
        }
    }

    public static void SetMousePos(Vector3 pos) { MousePos = pos; MouseOverride = true; }

    public static void ClearAll()
    {
        Held.Clear();
        Down.Clear();
        Consumed.Clear();
        MouseHeld.Clear();
        MouseDown.Clear();
        MouseOverride = false;
    }

    public static void EndFrame()
    {
        if (!Enabled) return;
        // Down is consumed by the first GetKeyDown caller. Keep it until then so the
        // probe's one-frame script pulses survive Unity's component update order.
        MouseDown.Clear();
    }
}

[HarmonyPatch(typeof(Input), "GetKey", new[] { typeof(KeyCode) })]
static class ProbeInputGetKey
{
    static bool Prefix(KeyCode key, ref bool __result)
    {
        if (!ProbeInput.Enabled) return true;
        __result = ProbeInput.Held.Contains(key);
        return false;
    }
}

[HarmonyPatch(typeof(Input), "GetKeyDown", new[] { typeof(KeyCode) })]
static class ProbeInputGetKeyDown
{
    static bool Prefix(KeyCode key, ref bool __result)
    {
        if (!ProbeInput.Enabled) return true;
        if (ProbeInput.Down.Contains(key) && !ProbeInput.Consumed.Contains(key))
        {
            ProbeInput.Consumed.Add(key);
            __result = true;
        }
        else __result = false;
        return false;
    }
}

[HarmonyPatch(typeof(Input), "GetMouseButton")]
static class ProbeInputGetMouseButton
{
    static bool Prefix(int button, ref bool __result)
    {
        if (!ProbeInput.Enabled) return true;
        __result = ProbeInput.MouseHeld.Contains(button);
        return false;
    }
}

[HarmonyPatch(typeof(Input), "GetMouseButtonDown")]
static class ProbeInputGetMouseButtonDown
{
    static bool Prefix(int button, ref bool __result)
    {
        if (!ProbeInput.Enabled) return true;
        __result = ProbeInput.MouseDown.Contains(button);
        return false;
    }
}

[HarmonyPatch(typeof(Input), "get_mousePosition")]
static class ProbeInputMousePosition
{
    static bool Prefix(ref Vector3 __result)
    {
        if (!ProbeInput.Enabled || !ProbeInput.MouseOverride) return true;
        __result = ProbeInput.MousePos;
        return false;
    }
}

[HarmonyPatch(typeof(PlayerCamera), "Update")]
public static class ProbePlayerUpdate
{
    static void Postfix() { if (!ReferenceEquals(Probe.Instance, null)) Probe.Instance.Tick(); }
}
