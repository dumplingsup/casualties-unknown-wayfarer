using System;
using UnityEngine;

namespace WayfarerMech
{
    /// <summary>
    /// Minimal readable assembly panel. The real deployed mech is rebuilt live in the
    /// world while the panel is open, so every selection is a genuine preview (including
    /// the player's real head in the open cockpit), not a card mock-up.
    /// </summary>
    public sealed class AssemblyPanel : MonoBehaviour
    {
        public static bool IsOpen { get; private set; }

        private MechLoadout draft, original;
        private MechSlot selectedSlot = MechSlot.Body;
        private Body previewPilot;
        private string message = "";
        private float messageUntil;
        private float closeGuardUntil;
        private int partPage;
        private const int PartsPerPage = 7;
        private Rect panel = new Rect(18, 40, 880, 620);

        private static readonly string[] SlotNames = { "机体 BODY", "左臂 LMB", "右臂 RMB", "腿 LEGS", "推进器 BOOSTER", "喇叭 HORN" };

        private void Update()
        {
            if (!Plugin.Ready) { if (IsOpen) Close(true); return; }
            if (IsOpen)
            {
                if (Plugin.Piloting || !Plugin.Mech || !Plugin.Mech.Grounded)
                {
                    Close(false);
                    return;
                }
                // The opening key press is also visible to this Update in the real
                // input path; do not let the same P keydown close the panel instantly.
                if (Time.unscaledTime < closeGuardUntil) return;
                if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
                {
                    Apply();
                    return;
                }
                if (Input.GetKeyDown(Plugin.AssemblyKey.Value) || Input.GetKeyDown(KeyCode.Escape))
                {
                    Plugin.LogInfo("ASSEMBLY_CLOSE_KEY assembly=" + Input.GetKeyDown(Plugin.AssemblyKey.Value) +
                                   " esc=" + Input.GetKeyDown(KeyCode.Escape) + " t=" + Time.unscaledTime);
                    Close(true);
                }
                return;
            }
            if (!Plugin.InputReady() || Plugin.Mech == null || Plugin.Piloting) return;
            if (Input.GetKeyDown(Plugin.AssemblyKey.Value)) TryOpen();
        }

        private void TryOpen()
        {
            if (!Plugin.Mech) { Plugin.Tell("先按 G 召唤机甲，停稳后再装配 / Summon the mech first"); return; }
            if (Plugin.Mech.Pilot) { Plugin.Tell("驾驶中不能换装 / Exit the cockpit first"); return; }
            if (!Plugin.Mech.Grounded) { Plugin.Tell("机甲停稳后才能装配 / Land the mech first"); return; }
            original = Plugin.Mech.Loadout != null ? Plugin.Mech.Loadout.Clone() : MechLoadout.Default();
            draft = original.Clone();
            selectedSlot = MechSlot.Body;
            partPage = 0;
            previewPilot = PlayerCamera.main ? PlayerCamera.main.body : null;
            message = "选择部件；机甲在背景中实时预览";
            messageUntil = Time.unscaledTime + 4f;
            if (!Plugin.Mech.ApplyPreviewLoadout(draft, previewPilot, out var reason))
            {
                Plugin.Tell("装配预览失败：" + reason);
                return;
            }
            IsOpen = true;
            closeGuardUntil = Time.unscaledTime + 0.5f;
            Plugin.LogInfo("ASSEMBLY_OPEN " + draft.Summary);
        }

        private void Close(bool restore)
        {
            if (!IsOpen) return;
            if (restore && Plugin.Mech && original != null)
            {
                if (!Plugin.Mech.ApplyPreviewLoadout(original, null, out var reason))
                    Plugin.LogError("ASSEMBLY_RESTORE_FAILED " + reason);
            }
            else if (Plugin.Mech)
            {
                Plugin.Mech.DetachPreviewPilot();
            }
            IsOpen = false;
            previewPilot = null;
            Plugin.LogInfo("ASSEMBLY_CLOSE restore=" + restore + " loadout=" + (Plugin.Mech ? Plugin.Mech.Loadout.Summary : "none"));
        }

        private void Apply()
        {
            if (!Plugin.Mech || draft == null) { Close(false); return; }
            draft.Normalize(out var reason);
            if (reason != null) { SetMessage(reason); return; }
            var player = PlayerCamera.main ? PlayerCamera.main.body : null;
            if (player) MechLoadoutStore.Set(player, draft.Clone());
            Plugin.Mech.DetachPreviewPilot();
            IsOpen = false;
            previewPilot = null;
            Plugin.Tell("装配已应用：" + draft.Summary);
            Plugin.LogInfo("ASSEMBLY_APPLIED " + draft.Summary + " palette=" + draft.PaletteId);
        }

        private void SelectPart(MechPartDef part)
        {
            if (part == null || Plugin.Mech == null) return;
            var before = draft.Get(selectedSlot);
            draft.Set(selectedSlot, part.Id);
            if (!Plugin.Mech.ApplyPreviewLoadout(draft, previewPilot, out var reason))
            {
                draft.Set(selectedSlot, before);
                SetMessage(reason);
                return;
            }
            SetMessage("已预览 " + part.Name);
        }

        private void SelectPalette(string palette)
        {
            var before = draft.PaletteId;
            draft.PaletteId = palette;
            if (!Plugin.Mech.ApplyPreviewLoadout(draft, previewPilot, out var reason))
            {
                draft.PaletteId = before;
                SetMessage(reason);
                return;
            }
            SetMessage("配色 " + MechCatalog.PaletteNames[palette]);
        }

        private void SetMessage(string text)
        {
            message = text;
            messageUntil = Time.unscaledTime + 3.5f;
        }

        private void OnGUI()
        {
            if (!IsOpen || !Plugin.Ready || !Plugin.Mech || draft == null) return;
            GUI.depth = -20;
            // Keep the logical layout readable at smaller window sizes.
            var previousMatrix = GUI.matrix;
            float uiScale = Mathf.Min(1f, Mathf.Min(Screen.width / 920f, Screen.height / 700f));
            GUI.matrix = Matrix4x4.Scale(new Vector3(uiScale, uiScale, 1f));
            try
            {
            GUI.DrawTexture(panel, Texture2D.whiteTexture, ScaleMode.StretchToFill, false, 0f,
                new Color(0.07f, 0.085f, 0.10f, 0.97f), 0f, 0f);
            GUI.Box(panel, "");
            GUI.Label(new Rect(panel.x + 14, panel.y + 8, 600, 26), "<b>WAYFARER ASSEMBLY  /  模块机甲装配</b>");
            if (GUI.Button(new Rect(panel.x + panel.width - 92, panel.y + 8, 78, 24), "关闭 X"))
            {
                Plugin.LogInfo("ASSEMBLY_CLOSE_X t=" + Time.unscaledTime);
                Close(true);
                return;
            }
            GUI.Label(new Rect(panel.x + 14, panel.y + 30, panel.width - 28, 20),
                "Enter 应用 / P/Esc 取消 · 应用前检查新机体净空 · 驾驶中不可换装");

            float leftX = panel.x + 14;
            float leftY = panel.y + 82;
            for (int i = 0; i < 6; i++)
            {
                var slot = (MechSlot)i;
                bool active = slot == selectedSlot;
                var style = new GUIStyle(GUI.skin.button) { alignment = TextAnchor.MiddleLeft, fontSize = 13 };
                if (active) style.normal.textColor = new Color(1f, 0.83f, 0.35f);
                string id = draft.Get(slot) ?? "";
                string label = SlotNames[i] + "\n" + (string.IsNullOrEmpty(id) ? "—" : id);
                if (GUI.Button(new Rect(leftX, leftY + i * 58, 148, 50), label, style))
                {
                    selectedSlot = slot;
                    partPage = 0;
                    SetMessage("选择 " + SlotNames[i]);
                }
            }

            float midX = panel.x + 178;
            float midY = panel.y + 82;
            var parts = Candidates(selectedSlot);
            int pages = Mathf.Max(1, (parts.Count + PartsPerPage - 1) / PartsPerPage);
            partPage = Mathf.Clamp(partPage, 0, pages - 1);
            GUI.Label(new Rect(midX, midY - 22, 350, 20),
                "可用部件 / PARTS  " + (partPage + 1) + "/" + pages + "  (" + parts.Count + ")");
            int row = 0;
            for (int i = partPage * PartsPerPage; i < parts.Count && row < PartsPerPage; i++, row++)
            {
                var part = parts[i];
                bool selected = draft.Get(selectedSlot) == part.Id;
                var style = new GUIStyle(GUI.skin.button) { alignment = TextAnchor.MiddleLeft, fontSize = 13 };
                if (selected) style.normal.textColor = new Color(1f, 0.83f, 0.35f);
                var rect = new Rect(midX, midY + row * 48, 350, 44);
                var icon = MechArt.Get(part.IconKey);
                if (icon != null && icon.Sprite)
                    GUI.DrawTexture(new Rect(rect.x + 6, rect.y + 4, 36, 36), icon.Sprite.texture, ScaleMode.ScaleToFit);
                if (GUI.Button(new Rect(rect.x + 48, rect.y, rect.width - 48, rect.height),
                        part.Name + "\n" + PartSubtitle(part), style))
                {
                    SelectPart(part);
                }
            }
            if (parts.Count == 0) GUI.Label(new Rect(midX, midY, 350, 30), "没有可用部件（空槽合法）");
            if (pages > 1)
            {
                float pageY = midY + PartsPerPage * 48 + 4;
                if (GUI.Button(new Rect(midX, pageY, 96, 26), "◀ 上页"))
                    partPage = Mathf.Max(0, partPage - 1);
                if (GUI.Button(new Rect(midX + 104, pageY, 96, 26), "下页 ▶"))
                    partPage = Mathf.Min(pages - 1, partPage + 1);
                GUI.Label(new Rect(midX + 208, pageY + 3, 140, 22),
                    (partPage * PartsPerPage + 1) + "–" +
                    Mathf.Min(parts.Count, (partPage + 1) * PartsPerPage) + " / " + parts.Count);
            }

            float rightX = panel.x + 548;
            float rightY = panel.y + 60;
            GUI.Label(new Rect(rightX, rightY, 310, 22), "当前机体 / LIVE PREVIEW");
            AssemblyPreview.Draw(new Rect(rightX, rightY + 26, 310, 230), Plugin.Mech);
            var stats = MechCatalog.Combine(draft);
            GUI.Label(new Rect(rightX, rightY + 264, 310, 24), "属性 / STATS");
            GUI.Label(new Rect(rightX, rightY + 290, 150, 100),
                "速度 ×" + stats.MoveMul.ToString("0.00") + "\n" +
                "跳跃 ×" + stats.JumpMul.ToString("0.00") + "\n" +
                "质量 " + stats.Mass.ToString("0"));
            GUI.Label(new Rect(rightX + 156, rightY + 290, 150, 100),
                "推力 ×" + stats.ThrustMul.ToString("0.00") + "\n" +
                "升力 ×" + stats.RiseMul.ToString("0.00"));
            GUI.Label(new Rect(rightX, rightY + 358, 300, 22), "配色 / PALETTE");
            for (int i = 0; i < MechCatalog.Palettes.Length; i++)
            {
                string palette = MechCatalog.Palettes[i];
                var old = GUI.color;
                GUI.color = MechCatalog.PalettePrimary[palette];
                if (GUI.Button(new Rect(rightX + i * 76, rightY + 384, 68, 30),
                    draft.PaletteId == palette ? "●" : "")) SelectPalette(palette);
                GUI.color = old;
                GUI.Label(new Rect(rightX + i * 76, rightY + 416, 72, 22), palette);
            }
            GUI.Label(new Rect(panel.x + 14, panel.y + 500, 520, 36),
                "预览不会保存；应用后写入当前角色存档。取消恢复原配置。");

            if (GUI.Button(new Rect(panel.x + panel.width - 250, panel.y + panel.height - 48, 110, 34), "应用 APPLY"))
            {
                Plugin.LogInfo("ASSEMBLY_APPLY_BUTTON t=" + Time.unscaledTime);
                Apply();
                return;
            }
            if (GUI.Button(new Rect(panel.x + panel.width - 130, panel.y + panel.height - 48, 110, 34), "取消 CANCEL"))
            {
                Plugin.LogInfo("ASSEMBLY_CANCEL_BUTTON t=" + Time.unscaledTime);
                Close(true);
                return;
            }
            if (!string.IsNullOrEmpty(message) && Time.unscaledTime < messageUntil)
                GUI.Label(new Rect(panel.x + 16, panel.y + panel.height - 42, panel.width - 290, 30), message);
            }
            finally { GUI.matrix = previousMatrix; }
        }

        private static System.Collections.Generic.List<MechPartDef> Candidates(MechSlot slot)
        {
            var result = new System.Collections.Generic.List<MechPartDef>();
            if (slot == MechSlot.ArmLeft || slot == MechSlot.ArmRight)
                result.AddRange(MechCatalog.All(MechSlot.ArmLeft));
            else
                result.AddRange(MechCatalog.All(slot));
            return result;
        }

        private static string PartSubtitle(MechPartDef part)
        {
            switch (part.Slot)
            {
                case MechSlot.Body:
                    return "机体 " + part.HullW.ToString("0.0") + "×" + part.HullH.ToString("0.0") + " · 速度×" + part.MoveMul.ToString("0.00");
                case MechSlot.ArmLeft:
                case MechSlot.ArmRight:
                    return part.Behavior.ToString() + " · 射程×" + part.RangeMul.ToString("0.00") + " · 伤害×" + part.TerrainMul.ToString("0.00");
                case MechSlot.Legs:
                    return "速度×" + part.MoveMul.ToString("0.00") + " · 跳跃×" + part.JumpMul.ToString("0.00");
                case MechSlot.Booster:
                    return "推力×" + part.ThrustMul.ToString("0.00") + " · 升力×" + part.RiseMul.ToString("0.00");
                case MechSlot.Horn:
                    return part.Id == "none" ? "合法空槽" : "独立按键";
            }
            return "";
        }
    }
}
