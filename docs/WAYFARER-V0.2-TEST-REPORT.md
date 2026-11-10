# Wayfarer 机甲 v0.2.0 实现与测试报告

日期：2026-11-10（Asia/Hong_Kong）。本文对应最终构建 DLL：

```text
WayfarerMech.dll SHA-256: 7761b8d46f4571e2b72c2f414b901b22bc05d03374e086965d9db6f2596463fe
文件大小: 67,584 bytes
游戏: Casualties: Unknown, Steam build 26525321, Unity 2022.3.62f3 Mono
依赖: BepInEx 5.4.23.6 / CUCoreLib 1.1
```

交付物：

- Mod-only：`artifacts/WayfarerMech-v0.2.0.zip`（37,723 bytes，SHA-256 `5e6319dbc9f7df24a294921ad7b13b996014cea1f562773e280fa4f9d13e8277`）
- 源码/原创资产/报告：`artifacts/WayfarerMech-v0.2.0-source.zip`（69,012 bytes，SHA-256 `01ec1287745bfe26bcf1c11c1fe2b821ff8dc06903665cc42194af93a66201bd`；含 `src/WayfarerMech`、资产 PNG/生成器、纯逻辑测试、本报告与 `ART-ASSETS.md`）
- 最小组合 stage：`artifacts/wayfarer-v0.2.0/`（24 个文件，`verify` PASS）
- 可回退 v0.1：`versions/wayfarer-0.1.0/`（源码、v0.1 DLL、旧 stage lock）
- 源码、资产生成器、纯逻辑测试：`src/WayfarerMech/`、`tools/art/`、`tests/DrillShapeTests/`

本报告区分：静态、构建、stage、启动、真实交互、存读档、外观、性能、兼容/多人。**未执行或证据不足的项目不写 PASS。**

---

## 1. 结论摘要

| 项目 | 结果 | 依据 |
|---|---|---|
| 静态/构建 | **PASS** | Release 0 警告 0 错误；无自动部署 Target；GUID/物品 ID 未变；资产嵌入 |
| Stage 文件 | **PASS** | `cumod lock/stage/verify`，24 文件，最终 DLL 哈希 7761… |
| 启动/注册 | **PASS** | 隔离沙盒启动，Wayfarer 0.2.0 与 CUCoreLib 1.1 加载；无缺图/注册错误；控制台目录和 `CUCoreLib Bulk injected 1 items` 正常 |
| 召唤/上机 | **PASS** | 真实 G/F 输入；`MECH_SUMMONED`、`PILOT_ENTER`；玩家 renderer/collider/rb 隐藏，退出恢复 |
| 持续喷气 | **PASS** | 真实长按 3 s，净上升 **71.359** 世界单位，最大上升速度 24.9；松开后自然下落 |
| 短跳/空中再启动 | **PASS** | 短按上升 5.885；下降中重新按住，vy 从 -31.2 制动并转正到 +4.5 |
| 输入门禁 | **PASS** | 暂停/菜单时 InputReady=false；按住空格恢复后仍不喷气，松开重按才恢复 |
| 空中 F 下机 | **PASS** | 空中按 F 保持 piloting=true，无 PILOT_EXIT；地面 F 通过 nearby 安全点退出 |
| 钻头增伤/刷形 | **PASS（可控材料）** | 钢块 5000 → 首次破块 **209.77 ms**；耐热合金 15000 → **618.68 ms**；轻质岩 100 → 0 ms；实测 ID/名称/血量见 §5 |
| 可通行挖掘 | **PASS（水平）/ NOT_RUN（向上、斜向）** | 水平钻入天然岩壁后按 D 驶入，x 从 86.5 到 93.885；向上/斜向未完成正式用例 |
| 补丁/边界保护 | **PASS（代码/一次观察）** | 200 ms 内 24 格上限、深度 2、越界/去重/最近优先由纯测试覆盖；`cantHit`/`ignoreLoot` 保留 |
| 存读档（地面附近） | **PASS** | 保存→杀进程→重启→读档；信标保留、玩家可见、读档后可再次召唤/上机/飞行 |
| 存读档（高空） | **PASS** | 在机身高约 50 单位、悬空时保存；`SAFE_EXIT method=cached` 将驾驶者放到地面；读档后 beacon=1、玩家可见、存档被游戏删除 |
| 读档后再召唤（高空存档） | **FAIL（环境/生成点限制）** | 高空存档读档后，在本次出生舱按压 G 返回 `SPAWN_FAILED`；同一版本的另一次读档世界可正常再召唤/上机/飞行。属于生成点限制，不是存档字段缺失；已在“已知限制”注明 |
| 顶碰安全（不穿透/不弹飞） | **PASS（观察）** | 多次持续顶住顶棚/狭窄处 3–6 s，未穿透、未弹飞；速度被碰撞限制 |
| 顶碰检测/HUD/横移离顶 | **FAIL / NOT_RUN** | 一次宽顶棚夹具中 `ceilingBlocked=true`；另一次贴顶夹具中 `ceilingBlocked=false`，HUD/火焰状态不一致。横向离开顶面后继续飞升的完整用例未通过（机械安全仍在） |
| 外观 | **PARTIAL** | 游戏内截图可见分层机甲/驾驶舱/钻头/喷口/HUD；完整左右、行走、全向瞄准、火焰/落地矩阵未全部截图 |
| 性能 | **PARTIAL** | 60 fps 下钻掘 p50/p95 约 12–15 / 15–17 ms；软岩粒子节流已加；30/60/高帧率对照未完成 |
| 多人 | **NOT_RUN** | 未实现、未测；单机原型定位不变 |
| 其他 Mod/旧存档迁移 | **NOT_RUN** | 只测 BepInEx+CUCoreLib+Wayfarer 最小组合；v0.1 含信标存档升级未正式跑 |

---

## 2. 实现内容与保留契约

### 2.1 喷气

- 短按跳跃：`JumpSpeed=26`，仅在地面/可支撑状态触发一次，不在每帧重复赋初速。
- 长按阈值 `0.18 s`（游戏活动时间）；在空中重新按住也可启动。
- 净向上加速度 `ThrustNetAccel=35`：在 FixedUpdate 中补偿 `Physics2D.gravity.y * gravityScale` 后设置竖直速度，封顶 `MaxRiseSpeed=26`，最大下落 `40`。不修改全局重力、不依赖帧率。
- 顶碰：BoxCast/OverlapBox/逐点射线/接触点组合检测；顶住时停止向上推力和速度积累，离开后可继续。实际窄地形会出现侧向楔紧，见 §7。
- 暂停/菜单/控制台/失焦/下机/切层立即取消推力并置 `requireRelease`；恢复时必须松开再按。
- 燃料不限；无过热/耐久。

### 2.2 钻头

- 纯逻辑 `src/WayfarerMech/DrillShape.cs`，不依赖 Unity；`tests/DrillShapeTests` 共 20 项边界断言全过（水平、竖直、斜向、缺口、越界、深度、预算、去重、不可破坏边界、最近优先）。
- 刷形：以机体包围盒在挖掘方向的垂线投影 + 0.5 单侧余量确定列数；每列 DDA 从机壳外开始找**首个实体表面**，仅向内最多 2 格，不隔墙伤害。
- 预算：每脉冲最多 24 个唯一格（可配置），按到机体中心距离最近优先；越界过滤；低帧率不补发脉冲。
- 伤害：地形 2400/脉冲（与建筑/动物 180/80 分开配置），保留 `WorldGeneration.DamageBlock`、原生硬度、掉落和 `cantHit`；使用原生掉落。
- 观测：`BrokenBlocks`、`DrillPulses`、`LastPulseCandidates/Processed/Broken`、`LastDrillMs`、`AverageDrillMs`、`LastBlock/LastBreak` 均独立统计，没有把旧 `DrillHits` 冒充破坏格数。
- 音效：`Sound.Play` 仅在本模组同步地形循环内静音，避免每格原生爆音；喷气/钻探循环使用游戏原音频。软岩每脉冲即时破块数默认限制为 8（`MaxSoftBreaksPerPulse`），降低原生 DustBig 粒子峰值；硬块不受该限制，仍受 24 格预算限制。

### 2.3 外观/动画

- 原创分层 PNG，原生 PPU=8；部件：躯干、驾驶舱、背包、髋部、前后大腿/小腿/脚、钻臂、钻头（2 帧）、肩炮、炮口闪光（2 帧）、双喷焰（2 帧）、信标、阴影。生成器可编辑：`tools/art/gen_wayfarer_art.py`，预览/清单在 `tools/art/preview/`。
- 左右朝向只翻转视觉根；Rigidbody/Collider 不参与负缩放。关节 pivot、钻尖/炮口/喷口位置在 `MechVisual.cs` 与生成器一致。
- 行走采用分段步态（支撑/摆动、膝踝配合、脚底保持大致水平），非整腿正弦；钻头旋转/接触抖动、炮口后坐/闪光、喷气收腿/火焰、落地压缩都有实现。
- 武器从各自 pivot 向鼠标瞄准；射线起点使用实际钻尖/炮口。炮身限制在可读角度范围内，不上下颠倒。
- HUD 显示真实绑定（读取 `KeyBinds.GetBindName`），不盖关键原生状态栏；显示“喷气中/顶碰/驾驶中”。

### 2.4 安全退出与存档

- 驾驶者在接地时定期记录侧向安全点；保存/切层/紧急退出时：附近 → 重新校验缓存 → 地形扫描 → 当前（失败时记录 `SAFE_EXIT_FAILED` 并恢复玩家，不保持隐藏）。
- 普通空中 F 下机没有安全落点时拒绝并提示；不会把玩家直接丢在高空。
- 保存前同步把玩家放到安全地面，再走原版 SaveGame；机甲仍是临时实体，读档后重新召唤。GUID、信标 ID、召唤/驾驶/不损坏、暂停补丁、HUD 挂载方式（PlayerCamera.Update Postfix）均保留。
- 旧版信标迁移代码保留；未正式跑 v0.1 存档升级用例。

---

## 3. 构建与文件验证

```powershell
python tools/cumod.py doctor "D:\SteamLibrary\steamapps\common\Casualties Unknown"
dotnet restore src/WayfarerMech/WayfarerMech.csproj --locked-mode
dotnet build src/WayfarerMech/WayfarerMech.csproj -c Release --no-restore
dotnet run --project tests/DrillShapeTests/DrillShapeTests.csproj -c Release
python -m unittest discover -s tests -v
python tools/cumod.py lock profiles/wayfarer.local.json --out artifacts/wayfarer-v0.2.0.lock.json
python tools/cumod.py stage artifacts/wayfarer-v0.2.0.lock.json --out artifacts/wayfarer-v0.2.0
python tools/cumod.py verify artifacts/wayfarer-v0.2.0
```

结果：

- `Assembly-CSharp.dll` 哈希与基线一致：`de2462a8...67fc7335`；build `26525321`。
- Release 构建 0 警告 0 错误；无 build 自动复制 Target。
- 纯逻辑测试：`ALL TESTS PASSED`（20 项）。
- 管理器：13 个 unittest OK；lock/stage/verify 24 个文件 PASS。
- 交付 DLL 与沙盒内实际运行的 DLL 哈希一致：`7761b8...6463fe`。
- Mod-only ZIP 只含 `BepInEx/plugins/WayfarerMech/WayfarerMech.dll` 与 README；MechProbe、游戏 DLL、存档、反编译资料、原版提取图均未进入。

---

## 4. 实机环境与隔离证据

- 测试副本：`D:\testGround\furry\.local\cu-test\game`；用户数据：`.local\cu-test\userdata`。
- 该副本由 `tools/TestIsolation` 修改过 `Assembly-CSharp.dll` 与 `CUCoreLib.dll` 的 `persistentDataPath`/PlayerPrefs/成就路径；**这是有测试插桩的副本，不是正式游戏**。正式游戏目录未修改。
- 探针每 0.5 s 记录真实 `Application.persistentDataPath`（仍显示正式用户路径）与 `CU_TEST_DATA`；实际 `save.sv` 出现在隔离 userdata，正式用户 `save.sv` 前后哈希未变：`aad02cef...501fab`。
- 每轮记录：`Player.log`、`status.json`、`observations.jsonl`、截图，见 `runs/<run-id>/`。
- 测试进程都用各自 PID 启动/关闭；测试结束游戏已退出，没有覆盖正在运行的用户游戏（测试期间用户游戏未运行）。
- 测试用 `console.json` 自定义键（F6 `saveandquit`、F3 `tp`、F4 `fullbright`、F5 `heal`）和 MechProbe 场景夹具只存在于测试 userdata/测试插件，**不进入玩家包**。

### 4.1 测试夹具说明

`tools/MechProbe` 是观察插件，另提供少量场景夹具：F8 找开放竖井、F1 在宽洞室放顶棚、F10/F11/F12 在当前挖掘方向建可控材料墙。它们**只布置场景**；召唤、上机、长按空格、鼠标钻/炮、F 下机、暂停和存档都是真实键鼠入口（`SendInput`）。夹具不实现生产逻辑，不替代飞行/伤害结果。测试计划中的“真实键鼠入口”要求按此理解。

---

## 5. 真实交互结果

### 5.1 飞行（run `v02-arena2`；长按/短按）

- 短按 Space 120 ms：`pilotSession.maxY - startY = 5.885`，最大 vy 24.9，thrustFrames=0。
- 长按 3 s：起始 y=91.316，最大 y=162.675，**净上升 71.359**；最大 vy 24.9；窗口内持续 thrust；释放后 `thrustActive=false`，最大下落约 -41.1（配置 40 的上限附近）。
- 空中再启动：按住 0.9 s 上升 → 松开 0.8 s（vy 约 -31.2）→ 再按住；0.18 s 阈值后 `thrustActive=true`，vy 从 -31.2 制动并转正到 +4.5；证明空中重按可制动/再上升。
- 暂停门禁：暂停时 `inputReady=false`、y 不变；按住空格恢复后 `thrustActive=false`、y 不变；松开再按才恢复。
- 空中 F：悬空时按 F，状态保持 `piloting=true`，日志无 `PILOT_EXIT`；地面按 F 时 `SAFE_EXIT method=nearby pos=(83.10,91.32)`，玩家恢复 `simulated=true`、`forceNoMovement=false`。
- 顶碰：多次持续顶住顶棚/狭窄处 3–6 s，y 被限制，无穿透、无突然弹飞。检测/HUD 一致性问题见 §7。

### 5.2 钻掘/材料（run `v02-final10`）

测试夹具在当前朝向（右）建立 13×10 格可控材料墙；实际鼠标左键按住钻掘。首次有效伤害到该格销毁：

| 材料 ID/名称 | 初始硬度 | 实测首次销毁耗时 | 目标 | 结果 |
|---|---:|---:|---:|---|
| 1 轻质岩 | 100 | 0 ms（同脉冲） | 软岩一轮 | PASS |
| 6 钢块 | 5000 | **209.77 ms** | ≈0.4 s 内 | PASS |
| 10 耐热合金 | 15000 | **618.68 ms** | ≈1 s 内 | PASS |

- `DRILL_BREAK` 日志给出 ID、名称、血量、耗时、格坐标；`BrokenBlocks` 累计 99（钢/耐热/软岩测试合计），没有用 `DrillHits` 冒充。
- 软岩窗口 `brokenDelta=16 / 0.5 s`；`MaxSoftBreaksPerPulse=8` 生效；钻掘窗口帧时 p50 13.5 ms、p95 17.0 ms（比未节流的 ~21 ms 好）。
- 天然岩壁水平钻入 → 按 D 驶入：x 86.5 → 93.885，y 从 91.3 到 90.3；证明通道可通行（这次天然壁较薄，完整厚墙通道由夹具钢/合金墙验证钻掘，但未完成向上/斜向驶入）。
- 掉落：保留原生 `DamageBlock`，没有绕过掉落；截图/日志可看到原生破坏与碎片路径。未对所有材料逐项清点掉落数量。

### 5.3 存读档（run `v02-highsave1` / `v02-highreload1`）

**高空保存**（最终 DLL）：

- 在 85 格高竖井内真实按住 Space 2.2 s：保存前 y=-419.339，最大上升 50.345，`thrustActive=true`，vy=24.9，grounded=false。
- 按住空格同时触发真实键盘绑定 `F6`（userdata `console.json` 绑定游戏控制台命令 `saveandquit`；命令本身是游戏提供的真实命令，测试专用绑定）。
- 日志：

```text
LIFECYCLE_EXIT reason=save
SAFE_EXIT method=cached pos=(231.10,-469.68) mech=(234.50,-416.56)
PILOT_EXIT method=cached pos=(231.10,-469.68)
MECH_DESTROYED
```

- 保存文件：`runs/v02-highsave1/save-high.sv`，2,299 bytes，SHA-256 `00b75388cf1848ccfbcc5f44ed6cc412bd1f2150657d00495a556fa12e635708`；GZip JSON 内 `items` 含 `wayfarer_mech_beacon`，`cheatedSave=true`（`saveandquit` 的作弊标记，仅测试）。
- **进程重启读档**（`v02-highreload1`）：主菜单 → 继续探险 → 世界生成后 `status.json`：

```json
{"loadedRun":true,"player":{"x":0.344,"y":501.207,"conscious":true,
 "beaconCount":1,"simulated":true,"forceNoMovement":false}}
```

  `save.sv` 在成功读档后被游戏删除；玩家不在高空，机体未作为永久实体留存；没有出现隐藏/禁用身体。控制台因 `cheatedSave` 警告自动打开，关闭后 `inputReady=true`。
- 读档后在该世界按 G 再召唤返回 `SPAWN_FAILED origin=(0.37,501.19)`；这是狭窄出生舱的生成点失败，不是存档字段缺失。同版本在另一读档世界（`v02-reload1`）已完成再召唤、F 上机、Space 飞行（净上升 32.518）的证据。
- 早期一次地面附近保存/读档（`v02-reload1`）同样 PASS：beacon=1、玩家可见、读档后再召唤/上机/飞行正常。

**结论**：高空保存→退出→重启读档的主风险（裸玩家高空、隐藏身体、信标丢失）未观察到；安全退出走的是缓存地面点。读档后特殊出生舱的再召唤失败已如实记录，不能据此宣称所有起始场景都能立即再部署。

---

## 6. 外观检查（PARTIAL）

游戏内截图证据：

- `runs/v02-smoke4/shot-board.png`：侧面分层机体、背部包、钻臂、驾驶舱窗、HUD。
- `runs/v02-arena2/shot-board.png`、`shot-longflight.png`、`shot-drill1.png`、`shot-drive.png`。
- `runs/v02-final10/shot-materials.png`：夹具钢/合金墙与钻掘缺口。
- `runs/v02-highsave1/shot-world.png`：读档后世界界面。
- 设计期合成预览：`tools/art/preview/composite_game_shot08.png`（对早期 v0.1 游戏截图的 3× 比例合成，非最终验收截图）。

已观察：左右翻转/钻尖/炮口随鼠标、侧视结构、低饱和钢/橄榄/锈/琥珀配色；无缺图/粉色占位；HUD 中文可读且未盖底栏。未系统截齐左右站立、行走、全向瞄准、喷气火焰、落地压缩、暗/亮洞室对照；这些标记 PARTIAL/NOT_RUN。素材来源与 atlas 参考 row_id 见 `docs/ART-ASSETS.md` 与 `tools/art/preview/manifest.json`（无 atlas 像素直接拷入）。

---

## 7. 已知缺陷/限制（必须随包说明）

1. **顶碰标志不完全一致**：顶棚/狭窄处的实际碰撞安全（不穿透、不弹飞）成立，但在部分贴顶/角落几何中 `ceilingBlocked` 仍为 false，导致 HUD/火焰显示“仍在推力”而实际无净上升。横向离开顶面后继续上升的完整用例未通过。原因与 5 号的侧向楔紧/碰撞法线有关；没有用修改物理尺寸或挖空地形来掩盖。
2. **生成点**：出生舱空间很窄时 G 可能返回“附近空间不足/SPAWN_FAILED”，或回退把机甲投到较低平台；玩家可 G 回收后换开阔位置。不能把“可召唤”包装成“任何位置都能召唤”。
3. **向上/斜向通道**未完成正式驶入用例；水平通道和材料破坏已完成。
4. **30/60/高帧率对照**未完成；60 fps 下飞行/钻掘有数据。
5. **战斗目标**：右键机炮保留 v0.1 的 `ignoreBody=true`，对地形/建筑有效，对角色身体不造成伤害；没有声称对动物/敌人已做完整伤害回归。
6. **多人/其他 Mod/旧存档迁移**未测。
7. 测试环境是插桩沙盒；`saveandquit` 会禁用成就并写 `cheatedSave`，只用于测试。正式用户不应使用这个测试绑定。
8. 生成器/资产为原创代码；Atlas 只作参考、不打包。若未来商业分发，仍需按 `docs/PIXEL-ART-ATLAS-RESEARCH.md` 复核 Krea/CC0 上游条款。

---

## 8. 未通过/未运行项清单

| 项 | 状态 | 原因 |
|---|---|---|
| 顶碰标志 + 横移离顶继续飞 | FAIL / NOT_RUN | 见 §7.1 |
| 向上、斜向通道驶入 | NOT_RUN | 未完成正式夹具/真实驶入；水平和材料测试已过 |
| 30/60/高帧率定量对照 | NOT_RUN | 只有 60 fps 记录 |
| 完整动画/左右/明暗外观矩阵 | PARTIAL/NOT_RUN | 已有多张游戏内截图，未逐状态截齐 |
| 动物/角色战斗伤害 | NOT_RUN | 保留 v0.1 `ignoreBody=true`，只验证地形/建筑 |
| 多人 | NOT_RUN | 单机原型；无同步设计 |
| 其他 Mod 兼容矩阵 | NOT_RUN | 只测最小依赖组合 |
| v0.1 含信标存档升级 | NOT_RUN | 迁移代码保留，未跑正式用例 |
| 读档后特殊出生舱立即召唤 | FAIL（环境） | 生成点限制；另一读档世界再召唤/飞行 PASS |
| 无安全点时的最终兜底 | NOT_RUN | 代码有 `SAFE_EXIT_FAILED` 路径，未复现 |

---

## 9. 回退

- v0.1 源码/DLL/stage lock：`versions/wayfarer-0.1.0/`；原 stage：`artifacts/wayfarer-test1/`。
- v0.1 DLL 哈希（旧 stage）为 `889f78ab...0ceb62`；当前 v0.2 最终 DLL 为 `7761b8...6463fe`。
- 回退到 v0.1 时同时回退 DLL 与 profile/stage，并注意 v0.2 生成的存档仍可由原版读回（本版未新增永久存档字段；机甲是临时实体，信标 ID 未变）。未做 v0.1→v0.2→v0.1 的完整往返实测，降级不保证。

---

## 10. 复现命令与证据目录

```powershell
# 构建/测试
dotnet restore src/WayfarerMech/WayfarerMech.csproj --locked-mode
dotnet build src/WayfarerMech/WayfarerMech.csproj -c Release --no-restore
dotnet run --project tests/DrillShapeTests/DrillShapeTests.csproj -c Release
python -m unittest discover -s tests -v

# 文件组合
python tools/cumod.py lock profiles/wayfarer.local.json --out artifacts/wayfarer-v0.2.0.lock.json
python tools/cumod.py stage artifacts/wayfarer-v0.2.0.lock.json --out artifacts/wayfarer-v0.2.0
python tools/cumod.py verify artifacts/wayfarer-v0.2.0
```

关键证据：

| 内容 | 路径 |
|---|---|
| 最终长按/空中再启动/门禁/水平钻 | `runs/v02-arena2/status.json`, `observations.jsonl`, `Player.log`, `shot-*.png` |
| 最终材料 5000/15000/100 | `runs/v02-final10/status.json`, `Player.log`, `shot-materials.png` |
| 高空保存 + cached 安全退出 | `runs/v02-highsave1/Player.log`, `save-high.sv` |
| 高空读档 | `runs/v02-highreload1/status.json`, `Player.log` |
| 早期读档后再召唤/上机/飞行 | `runs/v02-reload1/status.json`, `Player.log` |
| 外观/HUD | `runs/v02-smoke4/shot-board.png`, `runs/v02-final10/shot-materials.png`, `runs/v02-highsave1/shot-world.png` |
| 构建/组合 | `artifacts/wayfarer-v0.2.0/`, `artifacts/wayfarer-v0.2.0.lock.json` |

本报告不把“构建成功”“文件 verify 成功”“截图存在”当作游戏玩法或读档通过；上述 PASS 均有对应的真实输入/状态/日志观察，未完成项保持 NOT_RUN/FAIL。
