# Wayfarer Mech v0.4 美术资产与来源

日期：2026-11-10。当前完整目录版本为 `WayfarerMech.dll` v0.4.0；旧 v0.2/v0.3 资产说明
保留在文末历史。所有发布 PNG 都是生成器从零绘制的原创像素图，不包含 Starbound 原始像素。

## 1. 当前目录与资产规模

`src/WayfarerMech/Assets` 当前 **399 个 manifest 条目 / 400 个文件**（约 492 KB），
PPU=11、Point/Clamp，全部嵌入 DLL。目录由
`tools/build_starbound_catalog.py` 读取本机 Starbound item **ID/短名**后生成；
`tools/art/starbound_catalog.json` 是同一份目录数据，`tools/art/gen_starbound_style_mech.py`
读取它生成视觉。原始 Starbound 图片/配置/音频不在源包内。

| 槽位 | 运行时条目 | Starbound 条目 | 说明 |
|---|---:|---:|---|
| 机体 Body | 31 | 29 | 29 个 Starbound ID 全部可选 + Wayfarer 两个原创起点机体 |
| 臂 Left/Right | 30 | 27 | 27 个 Starbound 臂全部可选 + 3 个原创起点臂 |
| 腿 Legs | 10 | 8 | 8 个 Starbound 腿 + 2 个原创起点腿 |
| 推进器 Booster | 10 | 8 | 8 个 Starbound 推进器 + 2 个原创起点推进器 |
| 喇叭 Horn | 16 | 14 | 14 个 Starbound 喇叭 + 空槽/原创 siren |
| **合计** | **97** | **86** | 每个条目都有存档 ID、属性、图标和描述 |

- 29 个机体各有 `body_sb_<id>_back/front_<palette>` 四色原创重绘与唯一图标；主题色、
  羽冠/鸟喙/鳍/角/反应堆/面罩等装饰来自人工风格表。它们不是 Starbound 原图。
- 27 个 Starbound 臂映射到 4 个已实现行为族：`Drill`、`Melee`、`Cannon`、`BeamDrill`。
  导弹/地雷/护盾/无人机等使用 Cannon 适配族，描述明确写“适配，不是逐项弹道复刻”。
  `Melee` 是 v0.4 新增的独立近战行为（短射线/原生 Body/Building 伤害），已实际触发。
- 8 个 Starbound 腿/推进器/14 个喇叭共用原创视觉族（`leg_strider/hopper/arc/sleek`、
  `booster_vector/twinjet`、`horn_siren`），但 ID、属性、图标和存档独立；不把共享视觉
  冒充逐件原图。
- 装配台按槽位分页，每页 7 个；31 个机体可以全部浏览/选择。

## 2. 生成器与运行时

```powershell
python tools/build_starbound_catalog.py
python tools/art/gen_starbound_style_mech.py
dotnet build src/WayfarerMech/WayfarerMech.csproj -c Release
```

- `tools/build_starbound_catalog.py`：读取本机
  `items/generic/mechparts/**/*.item` 的 ID/短名，输出目录 JSON 和
  `src/WayfarerMech/StarboundCatalog.g.cs`；描述/风格/适配表是人工原创。
- `tools/art/gen_starbound_style_mech.py`：只画原创形状；不读取 Starbound PNG。
- 运行时：`MechArt.cs`（manifest/PPU/pivot）、`MechVisual.cs`（层/关节/排序/动画）、
  `MechCatalog.cs` + `StarboundCatalog.g.cs`（条目/属性和默认装配）。
- 机体 pivot `(28,30)`，画布 64×60；鸟喙在右侧，驾驶舱前层有透明开口，真实头/眼/上躯干
  由 `PilotVisualProxy` 在开口内同步绘制。

## 3. 来源、许可与 REA 记录

**发布包内不含 Starbound 原始素材。** Starbound / Chucklefish 名称与 ID 仅用于目录索引；
发布图是原创重绘。

- 本机 Starbound：`E:\SteamLibrary\steamapps\common\Starbound`。
- 官方 `asset_unpacker.exe` 解包到
  `E:\GameArtLibrary\starbound-mechs\source\raw`；清单
  `tools/starbound_assets_inventory.py` / `inventory.json`，430 个文件、485,941 字节。
  pak SHA-256 `325d7d42…3577fe`，unpacker `837e5759…e317e`。
- **REA 对 `packed.pak` 返回 unsupported binary format**；没有伪称 REA 成功打开 pak。REA
  用于本地素材/工件分析的限制已如实记录。没有下载不明第三方解包器。
- 原始解包目录、`tools/art/preview/ref_*`、`sb_*`、拼图和任何由原始像素生成的预览都不
  属于发布包；source ZIP 排除 `tools/art/preview` 目录。
- v0.2 的 Pixel Art Atlas 使用仅为人工风格参考（无像素拷贝），与本轮完整目录无关。

## 4. 验证

- `python -m unittest discover -s tests -v`：16/16，含 pivot/运行时契约。
- `runs/v040-catalog4` / `v040-catalog5`：
  `PROBE_CATALOG_AUDIT bodies=31 arms=30 legs=10 boosters=10 horns=15 keys=308 missing=0`；
  `PROBE_CATALOG_BUILD built=97 failed=0`。每个部件都在真实游戏进程中重建过视觉。
- `runs/v040-noprobe1`：无 Probe 启动，`399 entries` manifest 加载，无异常。
- 装配台截图见 `runs/v040-catalog5/shot-script-assembly.png`（分页 1/5、31 机体）。

## 5. 已知限制

- 86 个 Starbound 机体/臂/腿/推进器/喇叭条目中，特殊武器的弹道/AI 并非逐项复刻；只有
  4 个行为族。目录完整不等于每个原版特殊机制都完整。
- 机体视觉是同一鸟形骨架的主题/装饰变体，不是逐件 1:1 重画原版轮廓。
- 1280×720 下真实脸约 20–30 px；表情可读但不大。驾驶舱暖光/0.9 头缩放是折中。
- 视觉喙/羽冠略超出碰撞体；极窄通道可能出现挤压观感，未用缩放掩盖碰撞。
- 游戏底图暗，未做全地图光照矩阵。

## 6. 版本历史

- v0.4：完整 29 机体/27 臂/8 腿/8 推进器/14 喇叭目录，原创主题视觉，Melee 行为，
  分页装配，97 部件构建审计。
- v0.3：开放舱 + 2 机体/3 臂/2 腿/2 推进器/4 配色原型（`docs/WAYFARER-V0.3-…` 历史）。
- v0.2：早期侦察机甲、Pixel Art Atlas 参考；旧尺寸/偏移勿用于 v0.4。
