using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using CUCoreLib.Data;
using CUCoreLib.Registries;
using CUCoreLib.Saving;
using HarmonyLib;
using UnityEngine;

namespace WayfarerMech
{
    [BepInPlugin(Guid, "Wayfarer Mech", "0.4.8")]
    [BepInDependency("net.cucorelib", BepInDependency.DependencyFlags.HardDependency)]
    public sealed class Plugin : BaseUnityPlugin
    {
        public const string Guid = "local.cu.wayfarermech";
        public const string BeaconId = "wayfarer_mech_beacon";
        public static Plugin Instance;
        public static MechController Mech;
        public static ConfigEntry<KeyCode> SummonKey, BoardKey, AssemblyKey, HornKey;
        Harmony patches;
        Body knownBody;
        float grantAfter;
        public static bool Piloting => Mech && Mech.Pilot;
        public static bool IsPilot(Body body) => Piloting && Mech.Pilot == body;

        public static void LogInfo(string message) => Instance?.Logger.LogInfo(message);
        public static void LogError(string message) => Instance?.Logger.LogError(message);

        void Awake()
        {
            Instance = this;
            MechSettings.Bind(Config);
            SummonKey = Config.Bind("Controls", "SummonRecall", KeyCode.G, "Requires the beacon in your inventory. Recall only while outside.");
            BoardKey = Config.Bind("Controls", "EnterExit", KeyCode.F, "Enter nearby mech / leave immediately, including in mid-air.");
            AssemblyKey = Config.Bind("Controls", "Assembly", KeyCode.P, "Open the loadout panel while the mech is deployed, grounded and unoccupied.");
            HornKey = Config.Bind("Controls", "Horn", KeyCode.H, "Honk the optional horn while piloting.");
            ItemRegistry.Register(BeaconId, new CustomItemInfo
            {
                fullName = "Wayfarer mech beacon",
                description = "Use beacon or G to request/cancel a drop or recall an empty mech. After touchdown and unfolding, F boards or exits, even in midair. A/D move, jump to hop, hold jump to boost. LMB/RMB operate the arms, H sounds the horn. P opens assembly beside a parked mech.",
                category = "tool", weight = 0.1f, value = 0, usable = true,
                usableWithLMB = false, destroyAtZeroCondition = false, decayMinutes = 0,
                SpawnFrequency = 0, rec = new Recognition(0),
                useAction = (body, item) => { if (PlayerCamera.main && body == PlayerCamera.main.body) ToggleSummon(); }
            }, MechArt.Beacon());
            try
            {
                SaveRegistry.RegisterBodyProvider(MechLoadoutSaveProvider.Key, new MechLoadoutSaveProvider());
                LogInfo("Loadout save provider registered: " + MechLoadoutSaveProvider.Key);
            }
            catch (Exception ex)
            {
                LogError("Loadout save provider registration failed: " + ex.Message);
            }
            gameObject.AddComponent<AssemblyPanel>();
            patches = new Harmony(Guid);
            patches.PatchAll(typeof(Plugin).Assembly);
            LogInfo("Wayfarer 0.4.8 registered. Full modular catalogue; open-cockpit prototype; invulnerable chassis.");
        }

        public static bool Ready => PlayerCamera.main && PlayerCamera.main.body && WorldGeneration.world &&
            WorldGeneration.world.worldExists && !WorldGeneration.world.generatingWorld && !WorldGeneration.world.cutsceneActive;

        public static bool InputReady()
        {
            if (!Ready || PauseHandler.paused || Time.timeScale <= 0 || SettingsMenu.instance) return false;
            if (!Application.isFocused) return false;
            if (AssemblyPanel.IsOpen) return false;
            if (ConsoleScript.instance && ConsoleScript.instance.active) return false;
            var p = PlayerCamera.main;
            return !p.craftingPanel.activeSelf && !p.tradeMenu.activeSelf && !p.woundView.activeSelf &&
                !p.itemCustomizer.activeSelf && !p.radialOpen && !p.dragItem &&
                !(MinigameBase.main && MinigameBase.main.currentMinigame) && !LiquidTransfer.instance;
        }

        public void Tick()
        {
            if (!Ready) return;
            var body = PlayerCamera.main.body;
            if (knownBody != body) { knownBody = body; grantAfter = Time.unscaledTime + 0.5f; MechLoadoutStore.Ensure(body); }
            if (grantAfter > 0 && Time.unscaledTime >= grantAfter) { grantAfter = 0; GrantAtSpawn(body); }
            if (AssemblyPanel.IsOpen) return;
            if (!InputReady()) return;
            if (Input.GetKeyDown(SummonKey.Value) && !Piloting) ToggleSummon();
            if (Input.GetKeyDown(BoardKey.Value) && Mech && !Mech.Deploying)
            {
                if (Piloting) Mech.Exit(false);
                else if (Vector2.Distance(body.transform.position, Mech.transform.position) <= 7f) Mech.Enter(body);
                else Tell("驾驶舱距离过远");
            }
        }

        void GrantAtSpawn(Body body)
        {
            // Loaded characters receive a migration beacon only if none exists in their inventory.
            if (body && PlayerCamera.main && PlayerCamera.main.body == body && !HasBeacon(body))
            {
                var obj = Utils.Create(BeaconId, body.transform.position, 0);
                if (obj && obj.TryGetComponent<Item>(out var item))
                {
                    var slot = body.FirstEmptySlot();
                    if (slot.HasValue) body.PickUpItem(item, slot.Value, true);
                    if (!slot.HasValue) Tell("背包已满，召唤器已放在脚边");
                    LogInfo("BEACON_GRANTED body=" + body.GetInstanceID());
                }
                else LogError("Beacon spawn failed");
            }
        }

        public static bool HasBeacon(Body body) => body && body.GetComponentsInChildren<Item>(true).Any(x => x.id == BeaconId);

        public static void ToggleSummon()
        {
            if (!InputReady() || Piloting || AssemblyPanel.IsOpen) return;
            var body = PlayerCamera.main.body;
            if (!HasBeacon(body)) { Tell("请将机甲召唤器放在背包中 / Beacon required"); return; }
            if (Mech) { Mech.Recall(); return; }
            var loadout = MechLoadoutStore.Get(body);
            loadout.Normalize(out _);
            var stats = MechCatalog.Combine(loadout);
            if (!MechController.FindSpawn(body.transform.position, new Vector2(stats.HullW, stats.HullH), out var pos))
            { Tell("附近空间不足，请找开阔位置 / Need a clear area"); return; }
            var root = new GameObject("Wayfarer Mech");
            root.transform.position = pos;
            Mech = root.AddComponent<MechController>();
            Mech.BeginDrop();
            LogInfo("MECH_SUMMONED " + pos + " loadout=" + loadout.Summary);
        }

        public static void Tell(string message) { if (PlayerCamera.main) PlayerCamera.main.DoAlert(message); }

        void OnDestroy()
        {
            if (Mech) { Mech.Exit(true); Destroy(Mech.gameObject); }
            patches?.UnpatchSelf();
            MechArt.ReleaseAll();
            Instance = null;
        }
    }

    [HarmonyPatch(typeof(PlayerCamera), "Update")]
    static class PlayerRuntime
    {
        static void Postfix(PlayerCamera __instance)
        {
            if (ReferenceEquals(Plugin.Instance, null)) return;
            Plugin.Instance.Tick();
        }
    }

    [HarmonyPatch(typeof(PlayerCamera), "HandleInput")]
    static class DrivingInput
    {
        static bool Prefix()
        {
            if (AssemblyPanel.IsOpen) return false;
            if (!Plugin.Piloting) return true;
            if (!(ConsoleScript.instance && ConsoleScript.instance.active) && Input.GetKeyDown(KeyBinds.GetBind("pause"))) PauseHandler.TogglePause();
            return false;
        }
    }

    [HarmonyPatch]
    static class SuppressPilotActions
    {
        static IEnumerable<MethodBase> TargetMethods()
        {
            yield return AccessTools.Method(typeof(PlayerCamera), "HandleAttacks");
            yield return AccessTools.Method(typeof(PlayerCamera), "HandleDragging");
        }
        static bool Prefix() => !Plugin.Piloting && !AssemblyPanel.IsOpen;
    }

    [HarmonyPatch]
    static class FreezePilotBody
    {
        static IEnumerable<MethodBase> TargetMethods()
        {
            yield return AccessTools.Method(typeof(Body), "Update");
            yield return AccessTools.Method(typeof(Body), "FixedUpdate");
        }
        static bool Prefix(Body __instance) => !Plugin.IsPilot(__instance);
    }

    [HarmonyPatch(typeof(Limb), "Update")]
    static class FreezePilotLimbs { static bool Prefix(Limb __instance) => !Plugin.IsPilot(__instance.body); }

    [HarmonyPatch(typeof(SaveSystem), "SaveGame")]
    static class SafeSave
    {
        static void Prefix()
        {
            // Deployment is transient; the inventory beacon persists by the normal game path.
            // If there is no validated terrain spot, keep the pilot protected inside the mech
            // rather than placing the bare body at an unsafe current position.
            if (!Plugin.Mech) return;
            if (Plugin.Mech.Pilot)
            {
                bool exited;
                try { exited = Plugin.Mech.ExitForLifecycle("save", false); }
                catch (Exception ex) { Plugin.LogError("SAFE_EXIT_EXCEPTION " + ex); exited = false; }
                if (exited)
                {
                    UnityEngine.Object.Destroy(Plugin.Mech.gameObject);
                    Plugin.Mech = null;
                }
                else
                {
                    Plugin.Mech.RestorePilotStateForSaveHold();
                    Plugin.LogInfo("SAFE_SAVE_HOLD pilot remains inside mech; save will proceed without unsafe placement");
                }
            }
            else
            {
                UnityEngine.Object.Destroy(Plugin.Mech.gameObject);
                Plugin.Mech = null;
            }
        }

        static void Postfix()
        {
            // saveandquit returns from SaveGame before ToMainMenu; try one last safe exit.
            if (!Plugin.Mech || !Plugin.Mech.Pilot) return;
            bool exited;
            try { exited = Plugin.Mech.ExitForLifecycle("save-post", false); }
            catch (Exception ex) { Plugin.LogError("SAFE_EXIT_POST_EXCEPTION " + ex); exited = false; }
            if (exited)
            {
                UnityEngine.Object.Destroy(Plugin.Mech.gameObject);
                Plugin.Mech = null;
            }
        }
    }
}
