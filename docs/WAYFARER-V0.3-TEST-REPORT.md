# Wayfarer Mech v0.3.0 测试报告

日期：2026-11-10（Asia/Shanghai）。目标：按 `docs/STARBOUND-MECH-PORT-EXECUTION.md`
把开放舱、真实主角/表情、模块化换装、双臂战斗、顶碰与高空存读档安全做到可玩、可打包交付。

## 1. 结论摘要

- **已运行**：真实游戏进程（隔离 sandbox 副本）内的自动/合成输入路径、角色存读档、
  战斗、顶碰、自然地形召唤、60 秒驻留性能、无 Probe 启动。
- **未运行（如实标注）**：真实键盘/鼠标验收（Windows 会话锁定）；装配面板的真实指针
  点击/取消（仅用反射走 Apply）；真实友军 NPC 受击跳过（无友军体可用）；死亡/换层
  生命周期；多人/其他 Mod 组合。详见 §8。
- **包边界**：未上传 Steam Workshop，也未把 Starbound 原始素材或由其派生的预览图放进
  发布包。发布美术是脚本生成的原创像素图；Starbound 只作轮廓/配色参考。
- **正式存档未被改写**：`C:\Users\<user>\AppData\LocalLow\Orsoniks\CasualtiesUnknown\save.sv`
  全程保持 SHA-256 `AAD02CEFD5C55DC68345FAB7608E39A2DFA6A08B233453910B9A19A81A501FAB`
  （最后核对 2026-11-10）。正式游戏安装目录未改动。

## 2. 环境与构建

| 项 | 值 |
|---|---|
| 游戏 | Casualties: Unknown，Steam build 26525321，Unity 2022.3.62f3 Mono |
| 运行时依赖 | BepInEx 5.4.23.6 (x64)，CUCoreLib 1.1 |
| Mod | `WayfarerMech.dll` v0.3.0；插件 GUID `local.cu.wayfarermech`；信标 `wayfarer_mech_beacon` |
| 测试副本 | `D:\testGround\furry\.local\cu-test\game`（只读隔离副本，无正式改动） |
| 测试用户数据 | `D:\testGround\furry\.local\cu-test\userdata`（测试 DLL 重定向写盘） |
| 构建 | `dotnet build src\WayfarerMech\WayfarerMech.csproj -c Release` |
| 单元/契约测试 | `python -m unittest discover -s tests -v`：16/16 OK |

构建产物哈希和最终包哈希见交付清单 `artifacts/wayfarer-v0.3.0-delivery.json`。

最终候选（本报告编写时的干净构建）：

- `WayfarerMech.dll` v0.3.0：171,520 字节，SHA-256
  `E91BF006C709640FC3C72877859645A6D532290CEC446CAF23431541E3EE7233`
- `WayfarerMech-v0.3.0-candidate.zip`：89,178 字节，SHA-256
  `a3a05038a71e167629826b439a1fe96dc975a914cd7a2fdc73f7fba9533405eb`
- 完整 stage：`artifacts/wayfarer-v0.3.0-final/`，`cumod verify` = PASS，24 文件；
  `runtime_status` 字段为 `NOT_RUN` 仅表示 cumod 本身不启动游戏，实际运行证据见 §3–§9。

## 3. M0：Starbound 素材查找、提取与来源

- 本地 Starbound：`E:\SteamLibrary\steamapps\common\Starbound`。
- 使用的 **官方自带** `asset_unpacker.exe` 解包（REA 对 `.pak` 返回 unsupported binary
  format；未因此伪称 REA 成功）。解包结果只在本机参考目录：
  `E:\GameArtLibrary\starbound-mechs\source\raw`。
- 清单：`tools/starbound_assets_inventory.py` → `inventory.json`；共 **430** 个机甲相关文件，
  **485,941** 字节。源 pak SHA-256：`325d7d4210667b2a4738d805e858b80d7d55e8f834da773984ca6edb993577fe`；
  官方解包器 SHA-256：`837e57591a24fd1679b59e22c6f6aa9d6b556689b26cc8aa7b0ba9c5a90e317e`。
- 目标轮廓：Champion-00 / Avian proto：
  `vehicles/modularmech/body/avianproto_{back,front,fullbright}.png`，以及
  `items/generic/mechparts/body/mechbodyavianproto.*`。这些文件只做人眼轮廓/配色参考。
- **许可边界**：Starbound 素材版权归 Chucklefish；本仓库的发布包不包含其原始 PNG、其
  派生拼图或预览。`tools/art/preview/ref_*`、`sb_*` 和原始解包目录均被排除在发布包外。
  发布图由 `tools/art/gen_starbound_style_mech.py` 从零绘制；脚本头注释同样声明此事。

## 4. M1：Champion-00 开放舱与真实主角

实现：`PilotVisualProxy.cs` 在控制器隐藏玩家渲染器**之前**捕获 `FacialExpression`
的 head/eyes/nose、上躯干和可见配件，只复制 `SpriteRenderer`，不克隆
Body/Rigidbody/Collider/Item/Animator 或行为脚本。深度以 24–28 排序，头部缩放 0.9。

证据：

- `runs/v030-perf1/Player.log`：
  `PILOT_PROXY_BUILT face=True head=experimentHeadBack eyes=experimentEyeOpen
  torso=experimentUpTorso headMirrors=0 torsoMirrors=1`。
- `runs/v030-combat9/status.json`：`proxyEyes=experimentEyeClosed`（表情状态随游戏更新）；
  前序运行也观察到 `experimentEyeHalfClosed` / `experimentEyeLookBack`。这说明复制的是实际
  表情渲染器，不是贴死的贴图。
- `runs/v030-final-high2/shot-script-loadout.png` 及裁剪图：驾驶中的真实头部/面部与
  机甲开放舱同屏；旁边的粉色小生物是原生环境生物，不是玩家本体。
- `runs/v030-perf1/shot-auto-36.png`：默认骨白配色的完整机甲、平台与玩家。

限制：原生头部像素在 1280×720 下约 20–30 px；表情变化可观察，但不如全屏立绘夸张。
0.9 是“可读表情 vs 机甲比例”的人工折中。

## 5. M2：模块化机体/双臂/腿/推进器/配色/装配

数据驱动实现：`MechCatalog.cs`（6 个槽位）、`MechLoadout.cs`（每个 `Body` 的弱表存储）、
`MechVisual.cs`（按部件贴图/层级/关节构建）、`AssemblyPanel.cs`（IMGUI 装配台）、
`MechLoadoutSaveProvider.cs`（CUCoreLib Body provider）。

可换装内容：

| 槽位 | 选项 |
|---|---|
| 机体 Body | `champion_open`（4.6×6.9）、`venturer_open`（4.8×6.7） |
| 左/右臂 | `drill`、`cannon`、`beamdrill`（槽位不硬编码） |
| 腿 | `strider`、`hopper` |
| 推进器 | `vector`、`twinjet` |
| 喇叭 | `none`、`siren` |
| 配色 | `bone`、`olive`、`steel`、`violet` |

证据：

- `runs/v030-final-high2/Player.log`：从 `champion_open / drill / cannon / strider / vector`
  切换为 `ASSEMBLY_APPLIED Venturer-00 … beamdrill / drill / hopper / twinjet / siren /
  palette=olive`；`MECH_VISUAL_BUILT` 逐次重建；重新上机后 `hull=(4.80,6.70)`。
- 组件视觉/关节/近远臂排序由 `MechVisual` 重建；驾驶舱灯光、喷焰、钻头旋转帧、炮口
  闪光、光束 glow、喇叭音效均在测试中出现。
- `runs/v030-load3/Player.log`：`LOADOUT_RESTORE Venturer-00 … palette=olive`。

限制：装配面板的 Apply/Cancel 通过测试反射调用 public/private 方法验证；真实鼠标点击
未运行（桌面锁定；见 §8）。面板打开期间不暂停世界，符合“只能在停靠/地面使用”的提示逻辑。

## 6. M3：战斗、顶碰、高空退出与存读档

### 6.1 召唤/驾驶/自然地形

- 雕刻测试场：`runs/v030-perf1/Player.log` 的 `PROBE_ARENA`；骨白默认机体成功召唤、登机、
  驻留 69 秒。
- 未雕刻自然地形：`runs/v030-natural1/Player.log` 的 `PROBE_AUTO_NATURAL_SITE` 与
  `SPAWN_AIR_SHORT pos=(-10,505.62)`；机甲在自然洞穴中短距离落位，玩家成功登机，随后
  钻臂破坏 27 个方块。不是“只会在测试平台工作”。

### 6.2 喷气、移动、钻掘

- 长按 Space：`pilotSession.rise` 约 10–18 世界单位，`maxVy=24.9`（受 `MaxRiseSpeed`
  限制）；松开后回落。`runs/v030-ceiling6/status.json` 末帧 `grounded=true` 且推力关闭。
- 钻臂保留原生 `DamageBlock`/掉落/硬度路径：`runs/v030-natural1/status.json` 显示
  `brokenBlocks=27`，曾破坏钢块（health 5000）与耐热合金（15000）；测试场和自然场均观察到。
- 光束钻是独立 `ArmBehavior.BeamDrill`，有更远射程/不同伤害系数，不是把钻头贴图换色。

### 6.3 战斗

- **原生动物目标**：`runs/v030-combat9/Player.log`
  `MECH_TARGET_HIT kind=animal damage=198`，目标随后被移除；不是手写扣血。
- **原生建筑目标**：`MECH_TARGET_HIT kind=building damage=85` 两次，`铅灌丛` 被摧毁。
- **友军过滤**：`MechCombat.Cast` 先按 `BuildingEntity`/Ground/`Limb` 判定，友军返回
  `kind=friendly` 并且不进入 `DamageBody`；当前 `PROBE_FRIENDLY_PREDICATE self=True
  nullTarget=True`（见 `runs/v030-combat9/Player.log`）。由于没有可用的真实友军 NPC 体，
  “对友军实际命中不扣血”的目标级测试标记 NOT_RUN，而不是伪称通过。
- **自伤**：玩家本体是 `Pilot`；`IsFriendly` 对 self 返回 true；没有把玩家自己作为可击中
  Body 的测试。

### 6.4 顶碰

- 测试 fixture：`runs/v030-ceiling6`，真实静态 `BoxCollider2D` 板在同一 Ground 层，板底
  `y=474.1655`，机甲中心最高 `y=469.959`；连续 66 个采样帧 `CeilingBlocked=true` 且
  `ThrustActive=false`，说明推进器在接触前被提前关断，没有穿板。
- 移开 D 后：`ceiling=False thrust=True vy=24.9`，解除钳制。落地后 `ceilingBlocked=false`，
  没有旧的“站在地面误报顶碰”问题（旧的接触判定已加 `!Grounded` 门控）。
- 限制：测试板是夹具，不是全地图每个 tilemap 组合；斜坡/斜顶/窄管道只做了代码审查与
  短时窗口采样，未做完整矩阵。

### 6.5 高空退出与安全存档

- **高空 F**：`runs/v030-final-high2/Player.log`：
  `PROBE_HIGHSAVE_BEFORE mechPos=(102.50,503.35)`，随后
  `PROBE_HIGH_EXIT_F ok=False pilot=True`。在无侧向落脚点时不把裸玩家丢到空中，而是保持
  在受保护机舱并给出提示。
- **高空保存**：同一运行里 `SaveGame` 的 Prefix 调用
  `ExitForLifecycle("save", false)`；控制器通过向下地形扫描找到
  `(102.50,477.65)`，日志：
  `SAFE_EXIT method=terrain-scan` / `LIFECYCLE_EXIT reason=save ok=True method=terrain-scan`。
  保存后玩家在 y=477.615 的地面，机甲被临时实体销毁，状态 `piloting=false`。
- **读档**：从保存副本 `runs/v030-highsave3/save.sv` 载入（`runs/v030-load3`，以及用最终
  干净 DLL 复验的 `runs/v030-load-final`）：
  `loadedRun=true`，`LOADOUT_RESTORE` 恢复 Venturer/beamdrill/drill/hopper/twinjet/olive，
  `beaconCount=1`。基础游戏 `SaveSystem.SaveGame` 只写角色/物品/组件，不写世界坐标；
  读档后玩家回到本局默认出生点（x=0），这是**基础游戏行为**，不是本 Mod 把玩家丢到
  高空。用户可用信标重新召唤机甲。
- **真实存档隔离**：隔离测试的写盘位于 `.local/cu-test/userdata`；读档后游戏会按
  `SaveSystem.TryLoadGame` 删除 `save.sv`（基础游戏行为），所以测试保存副本另行保留在
  `runs/v030-highsave3/save.sv` 供检查。正式 `save.sv` 哈希未变（见 §1）。
- **无安全点 hold 分支**：`ExitForLifecycle(..., false)` 在 `FindForced` 也失败时保持驾驶者
  在机舱，并新增 `RestorePilotStateForSaveHold` 在保存前恢复 `forceNoMovement`，避免读档
  得到永久冻结的身体。该分支需要“全扫描范围无任何可行地面”的世界，未构造出真实场景，
  因此标记 NOT_RUN；相关状态恢复方法已静态审查。

### 6.6 无 Probe 启动

- `runs/v030-noprobe1`：移走 `MechProbe.dll` 后只保留 BepInEx 5.4.23.6 + CUCoreLib 1.1 +
  WayfarerMech 0.3.0。日志显示 `2 plugins to load`，Wayfarer 正常加载 manifest（112 项）
  并注册 provider，`Chainloader startup complete`，无 Mod exception。证明发布版不依赖测试探针。

## 7. 性能与稳定性

- `runs/v030-perf1`：默认骨白机体，登机后**驻留 69.2 秒**，无刷屏错误、无 Console 弹窗。
  最后窗口：`frames=60 / 0.506s`，`p50=8.092 ms`，`p95=10.809 ms`。
- 其他运行窗口的 p50/p95 约 7–12 ms；所有观察窗口都高于 60 FPS 的 16.7 ms 预算。
- 未做 30/60/高刷新率三档、长时多生物/多弹幕压力、Norwood 等硬件矩阵；标记 PARTIAL。

## 8. 未运行 / 阻塞（必须保留）

| 项目 | 状态 | 原因/边界 |
|---|---|---|
| 真实键盘/鼠标全流程 | **BLOCKED** | Windows 会话锁定：`LogonUI` 存在，`GetForegroundWindow()=0`；无法可靠把真实输入送到游戏。自动路径使用 `ProbeInput` 合成输入，本报告单列，不冒充真人操作。 |
| 装配面板真实鼠标 Apply/Cancel | **NOT_RUN** | 同上；Apply/Cancel 逻辑和换装结果用测试反射调用验证，未经指针/IMGUI 事件。 |
| 友军 NPC 实际命中不扣血 | **NOT_RUN** | 探针未找到可用友军 Body；只验证 self/null 谓词与代码路径。 |
| 死亡/失去意识/换层生命周期 | **NOT_RUN** | 需要可控触发玩家死亡或跨 biome 场景；未在隔离局中安全构造。 |
| 另一套同功能驾驶 Mod 冲突 | **NOT_RUN** | 未安装同类插件；已保留 GUID/信标契约，但没有实测。 |
| 多人/其他 Mod 组合 | **NOT_RUN** | 无多人测试环境和兼容清单。 |
| 全动画/全配色/全零件截图矩阵 | **PARTIAL** | 已覆盖骨白、橄榄、机关枪/光束/钻、两种腿/推进器/机体；喷气/走路/落地有截图，未逐一全帧比对。 |
| 高空 no-safe hold | **NOT_RUN** | 需要全扫描范围无地形的世界；未用破坏性世界构造。 |
| Steam Workshop 上传/审核 | **NOT_RUN / 未上传** | 用户未要求上传；只准备可上架包。 |

## 9. 证据索引

| 运行 | 内容 |
|---|---|
| `runs/v030-perf1` | 默认骨白、69s 驻留、p50/p95、无 Probe 前置 |
| `runs/v030-natural1` | 自然洞穴召唤、SPAWN_AIR_SHORT、钻掘 |
| `runs/v030-final-high2` | 高空 F 拒绝、SaveGame 地形扫描、机动/换装 |
| `runs/v030-highsave3` | 保存副本 `save.sv`、玩家落地、正式存档未变 |
| `runs/v030-load3` / `runs/v030-load-final` | 读档恢复 loadout、beaconCount=1；后者使用最终干净 DLL |
| `runs/v030-ceiling6` | 真实 collider 板、66 帧 blocked、解除钳制 |
| `runs/v030-combat9` | 原生 animal/building 伤害、友军谓词、表情 Closed |
| `runs/v030-noprobe1` | 无探针启动 smoke |
| `runs/v030-art1` | 早期骨白开放舱/换装链、可回看 |

所有路径相对于 `D:\testGround\furry`。测试游戏进程已关闭；正式游戏未启动或修改。
