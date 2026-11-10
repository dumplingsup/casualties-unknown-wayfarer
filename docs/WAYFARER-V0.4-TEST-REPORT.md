# Wayfarer Mech v0.4.0 测试报告（完整 Starbound 模块目录）

日期：2026-11-10。v0.4 在 v0.3 的开放舱/换装/战斗/安全存档基础上，加入了**完整
Starbound modular mech 部件目录**（本机可取得的全部 29 个机体、27 个臂、8 套腿、8 个
推进器、14 个喇叭）以及原创视觉适配。最终 DLL `WayfarerMech.dll` v0.4.0 为
652,288 bytes，SHA-256
`32629898F09C6C06D66BD42CD8FDCB9E8342B40D8BCC66AC3885FCFE74C38550`。
最终包哈希见 `artifacts/wayfarer-v0.4.0-delivery.json`。

## 1. 范围与诚实边界

- 用户请求是“整个 Starbound 机甲和机甲系统”。本轮把本机 Starbound 安装中
  `items/generic/mechparts` 的**全部部件 ID**纳入可选目录，并保留 Wayfarer 两个原创起点
  机体。目录计数：**31 机体 / 30 臂 / 10 腿 / 10 推进器 / 16 喇叭槽条目**。
- 特殊臂（导弹、地雷、护盾、无人机等）没有逐项复刻 Starbound 的弹道/召唤 AI；它们被
  映射到已实现的 `Drill / Melee / Cannon / BeamDrill` 行为族，并在每条描述中写明是
  “适配，不是对原版弹道的逐项复刻”。这是已实现的边界，不冒充 1:1 移植。
- 原始 Starbound PNG、配置、音频、派生预览都没有进入发布包；发布图由脚本从零生成。
  Starbound 名称/ID 仅作为目录索引与来源说明。详见 §7。

## 2. 完成判据与结果

| 层 | 结果 | 证据 |
|---|---|---|
| STATIC | PASS | `dotnet build` 0 warning / 0 error；`python -m unittest discover -s tests -v` 16/16 |
| CATALOG | PASS | `PROBE_CATALOG_AUDIT bodies=31 arms=30 legs=10 boosters=10 horns=15 keys=308 missing=0` |
| BUILD/INSTANTIATE | PASS | `PROBE_CATALOG_BUILD built=97 failed=0 firstFailure=`（97 个目录部件逐一构建视觉） |
| STAGE | PASS | `cumod verify artifacts/wayfarer-v0.4.0-final2` = PASS，24 files（最终 3262 DLL） |
| BOOT (no probe) | PASS | `runs/v040-noprobe1`：2 plugins，Wayfarer 0.4.0 / 399 art manifest，无异常 |
| PLAYER_VISUAL | PASS（自动化） | 真实 head/eyes/torso proxy；眼图 Open/Closed/HalfClosed/LookBack；换装后重登仍显示 |
| GAMEPLAY | PASS（见限制） | 召唤/驾驶/换装/双腿双臂独立、喷气、顶碰、原生破块/动物/建筑伤害 |
| SAVE_RELOAD | PASS（角色范围） | DOMINION / Solus Katana / Gatling / Arc / Zed 存档后 `LOADOUT_RESTORE`；正式存档哈希未变 |
| PERFORMANCE | PASS（单机/单场景） | v0.4 64.7 s 驻留，p50 8.47 / p95 10.31 ms；v0.3 69.2 s p50 8.1/p95 10.8 ms；未做多硬件矩阵 |
| REAL INPUT (physical) | **BLOCKED** | Windows 会话锁定（`LogonUI`、`GetForegroundWindow()=0`）；不能证明人手物理键鼠 |
| REAL INPUT (OS window messages) | PASS | 对真实 `CasualtiesUnknown` HWND 发送 WM_KEY/WM_MOUSE：D/Space 实际移动/上升 19、`thrustFrames=603`；F/P、装配台 Apply 均触发真实游戏代码（非 ProbeInput patch） |
| ASSEMBLY POINTER | PARTIAL | OS 鼠标点击 Apply 产生 `ASSEMBLY_APPLY_BUTTON`/`ASSEMBLY_APPLIED`；Cancel 坐标点击产生 `ASSEMBLY_CLOSE restore=True`；物理鼠标受桌面锁定限制 |
| WORKSHOP UPLOAD | **NOT_RUN** | 未上传；候选包/README/预览/文案已准备好，但上传需要明确授权 |

## 3. 完整目录与原创视觉

- 29 个 Starbound 机体 ID 全部成为 `sb_<id>` 条目（apex…skull，含 proto 系列）；加上
  `champion_open` / `venturer_open`，共 31 个机体。
- 27 个 Starbound 臂全部进入目录；`drill/chainsaw/energyblade/soluskatana/spikefist`
  走钻/近战族，光束族走 `BeamDrill`，枪/导弹/地雷/无人机/护盾走 `Cannon` 适配族。
  新增 `ArmBehavior.Melee` 并接入左/右键独立触发；目录中 `soluskatana` 已实际触发
  `MeleeArm`（`runs/v040-catalog5`）。
- 8 套 Starbound 腿、8 个推进器、14 个喇叭均有独立 ID/属性/图标/存档项；腿/推进器/喇叭
  使用现有/新生成的原创视觉族，不包含 Starbound 原始像素。
- 每个 Starbound 机体都有 `body_sb_<id>_back/front_<palette>` 四色原创重绘（主题色、
  鸟喙/羽冠、鳍/角/反应堆/面罩等装饰），生成器读取
  `tools/art/starbound_catalog.json`；该 JSON 由 `tools/build_starbound_catalog.py`
  从本机 item ID/短名和人工风格表生成，不包含原版图片。
- 装配台按槽位分页（每页 7 个，31 个机体共 5 页），显示属性、图标、配色、应用/取消；
  页码和“97 个部件全部可构建”的审计均已在隔离游戏内执行。

## 4. 运行时证据

- **全目录构建**：`runs/v040-catalog4` / `v040-catalog5`：
  `PROBE_CATALOG_AUDIT ... missing=0`；`PROBE_CATALOG_BUILD built=97 failed=0`。
  这不是只检查字符串：每个部件都真实走 `ApplyPreviewLoadout` 重建视觉/刚体/碰撞尺寸。
- **换装/存档**：`runs/v040-catalog4` 从 Champion 换到
  `Dominion / Solus Katana / Gatling Gun / Arc / Zed / steel`；`runs/v040-load1` 载入
  `loadedRun=true` 并 `LOADOUT_RESTORE` 同一套配置；beaconCount=1。
- **开放舱/表情**：`PILOT_PROXY_BUILT face=True`；proxy head/eyes/torso；眼图状态随游戏
  更新（Closed/Open/HalfClosed/LookBack 均出现过）。
- **战斗**：`runs/v040-combat1` 原生动物的 `MECH_TARGET_HIT kind=animal damage=198`，
  原生建筑 `kind=building damage=85`；友军谓词 `self=True nullTarget=True`。
- **顶碰**：`runs/v040-ceiling1` 板底 y=482.1655，连续 58 帧 `trueFrames=58`、
  `blockedThrustFrames=58`，机体最高 y=477.958；移开 D 后解除并上升。
- **高空退出/保存**：`runs/v040-highsave1` 高空 F `ok=False` 并保持驾驶；SaveGame
  通过 `method=terrain-scan` 从 y=485.35 落到 y=459.65，随后销毁临时机甲；
  `PROBE_HIGHSAVE_SAVE_CALLED`，正式存档哈希未变。
- **生命周期（无人为注入）**：`runs/v040-layer1` 驾驶中调用游戏自身
  `WorldGeneration.RegenerateWorld()`，真实层从 0 重新生成到 1：
  `PROBE_LAYER_START depth=0` → `MECH_SCENE_EXIT depth=0->1` →
  `SAFE_EXIT method=nearby` → `LIFECYCLE_EXIT reason=scene ok=True` →
  `MECH_DESTROYED`；`PROBE_LAYER_DONE depth=1 mech=False piloting=False`，玩家在
  新层安全出生。世界生成的 Sprite Tiling 警告是原版/测试场景噪声，不是本 Mod 异常。
  `runs/v040-lifecycle2` 另外测试了 consciousness=0 紧急下机和重新登机。
- **OS 窗口级键鼠**：`runs/v040-realinput2` 对真实 HWND 发送 WM_KEYDOWN/UP，
  D/Space 使机体实际移动并上升（`rise=19.07`、`maxVy=24.9`、`thrustFrames=603`）；
  `runs/v040-realmouse3` 对装配台真实窗口坐标点击 Apply，日志
  `ASSEMBLY_APPLY_BUTTON` → `ASSEMBLY_APPLIED`；点击 Cancel 坐标得到
  `ASSEMBLY_CLOSE restore=True`。这是 OS 窗口消息输入，不是物理人手；物理键鼠仍 BLOCKED。
  这轮据此发现并修复了“同一次 P keydown 立刻把面板关掉”的真实输入竞态
  （`AssemblyPanel.closeGuardUntil`）。
- **无 Probe 启动**：`runs/v040-noprobe1`，399 个 manifest 条目，无 Mod exception。
- **性能**：`runs/v040-perf2` 默认机体 boarded 64.7 s，最后窗口 p50 8.47 ms /
  p95 10.31 ms（59 frames / 0.502 s）；`v040-perf1` 的 54.6 s 窗口 p50 8.02 / p95 9.50
  为同 DLL 复测；v0.3 的 69.2 s 记录 p50 8.09 / p95 10.81（不同 DLL，仅作基线）。

## 5. 仍未运行 / 阻塞（不伪报）

| 项目 | 状态 | 说明 |
|---|---|---|
| 真实物理键盘/鼠标全流程 | **BLOCKED** | `LogonUI` 锁定、无前台窗口；不能证明人手物理输入。已用真实 HWND 的 OS 窗口消息验证键盘移动/喷气、F/P 和装配台 Apply/Cancel 坐标点击；需要用户解锁桌面后做最后的人手验收。 |
| 装配面板物理鼠标 Apply/Cancel/翻页 | **PARTIAL** | OS 鼠标窗口消息已触发 Apply 和 Cancel/restore；分页/部件行点击未逐一以物理鼠标确认。 |
| 真实友军 NPC 受击不扣血 | **NOT_RUN** | 探针未找到可生成/可定位的友军 Body；self/null 谓词通过。 |
| 真实换层（WorldGeneration 层切换） | **PASS** | `runs/v040-layer1` 调用真实 `RegenerateWorld()`，0→1；场景退出/安全下机/机甲销毁/新层出生均完成。 |
| 死亡（非 unconscious） | **NOT_RUN** | 只测了 consciousness=0 紧急退出，未走完整 death/respawn UI。 |
| 多人/其他 Mod 组合 | **NOT_RUN** | 无多人环境；未安装 Titan/同类驾驶插件做冲突矩阵。 |
| 特殊臂的逐项原版行为 | **PARTIAL/NOT_RUN** | 导弹/地雷/护盾/无人机的 Starbound 弹道/AI 未逐项实现，映射到 4 个行为族；文档已标注。 |
| 全组合/全配色截图矩阵 | **PARTIAL** | 31 机体/四色与 97 部件构建已验证；未对每套配色逐一截图和走完整动作。 |
| 60 s 满窗、多帧率、长时压力 | **PARTIAL** | 近 60 s 驻留一次；未做 30/60/高刷/多生物弹幕矩阵。 |
| Steam Workshop 上传/审核 | **NOT_RUN / 未上传** | 包与文案就绪；未调用任何上传工具，需用户明确授权。 |

## 6. 发布边界与正式环境

- 正式游戏目录未修改；正式
  `C:\Users\42173\AppData\LocalLow\Orsoniks\CasualtiesUnknown\save.sv` 保持
  `AAD02CEFD5C55DC68345FAB7608E39A2DFA6A08B233453910B9A19A81A501FAB`。
- 测试游戏/用户数据均在 `.local/cu-test`；测试进程已关闭。
- 候选包只含 `BepInEx/plugins/WayfarerMech/WayfarerMech.dll` 与 README；完整 stage 含
  BepInEx 5.4.23.6 + CUCoreLib 1.1。
- 未上传 Steam Workshop，也未修改正式游戏安装。
- 邻近 Titan 项目未修改。

## 7. 素材/许可

Starbound 原始素材未进入包。参考来源和哈希见 `docs/ART-ASSETS.md` 与
`tools/starbound_assets_inventory.py`。本机解包使用官方 `asset_unpacker.exe`；REA 对
`.pak` 返回 unsupported binary format，这一点在上一轮已记录，没有伪称 REA 成功。
发布视觉为原创重绘；Starbound ID/名称用于目录说明与兼容性索引。
