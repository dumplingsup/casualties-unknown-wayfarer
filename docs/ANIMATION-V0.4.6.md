# v0.4.6：重型步态与自由手动下机

2026-11-10。用户认可 v0.4.5 外观后指出小碎步、缺少重量感，并要求参考 Starbound 机甲，速度慢些、步伐夸张些。

## 最终实现

- 默认巡航速度 12 → 6 世界单位/秒，加速度 70 → 9，制动减速度 14。反向先刹到零再加速。驱动命令独立积分，避免低加速度被地面接触摩擦每帧清零。碰撞仍由原 Rigidbody2D/BoxCollider2D 处理。
- 原足端前后跨度 13px → 24px；抬脚上限 4px → 8px；完整步态循环从每移动 26px → 48px。使用机体实际位移推进，遇到障碍不空踩。
- 支撑相脚掌固定到世界位置，机体经过支撑脚；摆动相抬腿、前送、落脚。地面检测仍用于视觉 IK，而不是控制是否允许移动。
- 步行机体压低约 4px，随落脚回弹 1.2px，身体缓慢前倾；背包和工具有小于 1px 的错相跟随。外观、真实主角头/眼/表情代理继续保留。
- F 手动下机不再要求附近有地面，也不会因落脚点检查失败拒绝下机。优先选择侧方有身体空间的位置，均不满足时就在当前位置退出。空中离舱后遵从游戏重力。
- 保存/场景切换等自动生命周期安置与手动 F 分开，保留原存档保护。不更改存档 schema 和部件 ID。
- 设置迁移：新增 Ground.HandlingRevision=1，只将旧默认 MoveSpeed=12、HorizontalAccel=70 分别迁为 6、9，其他自定义数值保留。新增 HorizontalBrake=14。旧配置无需手动删除。

参考本机 Starbound `vehicles/modularmech/modularmech.lua` 的交替支撑、落脚机身回弹与工具错相跟随，以及 modularmech.vehicle 的步态参数。仅作运动设计参考，不引入 Starbound 代码、贴图或音频。

## 连续画面与失败记录

测试探针新增 CU_PROBE_MOTION=1：112 帧原始游戏截图，涵盖起步、右行、反向、左行、回转和停止；motion.jsonl 记录真实时间、机体和双脚世界坐标。工具 compare_motion.py 按实测时间采样成 15fps GIF，固定裁剪、最近邻放大，不调色、不慢放。截图发生在不同随机测试世界，照明与障碍不一致，不能当严格同场景性能对照。

- v046-motion-before：v0.4.5 旧 DLL。原手动下机在无落脚点时 exited=False。
- v046-motion-after：第一版，仅改 22px 步幅、6px 抬脚，仍为旧移动速度；用户要求更慢、更夸张后已被取代。
- v046-heavy：首版降低加速度后被接触摩擦抵消，实际不移动；**FAIL，不作为步态验收证据**。其对比图亦不用于交付。
- v046-heavy2：最终驱动与重型步态候选，结果见下表。
- v046-real-exit：第一版的真实 Windows F 输入专项，fixture 将机甲悬空，fakeinput=False，按 F 后 PILOT_EXIT method=manual，角色 simulated=True、forceNoMovement=False，实际下落。手动退出代码与最终候选一致。

原始动态对比里右行有遭遇障碍停住的区间；分析支撑与步幅时使用持续运动段，不将静止误判为改善。

## 验证分层

| 层 | 结果 |
|---|---|
| STATIC / 数学 | Python 19 项通过；姿态/步态 412 个断言，覆盖骨长、全周期可达性、支撑速度匹配、抬落脚连续性 |
| BUILD | Mod 与 Probe locked Release，0 warning / 0 error |
| STAGE | heavy2 lock/stage verify 24 文件通过 |
| 隔离 | 游戏 11、CUCoreLib 3 路径重定向；unsafe paths/prefs/achievements=0 |
| 最终实机 | PASS：112 帧采集与状态记录；约 0.8s 达实测速率 5.73；反向先归零，松键约 0.5s 停稳；无地面手动退出成功且玩家物理/移动恢复 |
| 最终重启读档 | PASS：v046-heavy-play，loadedRun=True；Champion、钻臂、机炮、巡行者腿、矢量推进器与 bone 配色保留；双脚接地、双手就位、真实头眼代理正常 |
| 真实键鼠 | 第一版悬空 F 下机通过；最终移动调参需结合本轮采集与用户体验，不宣称全部控制路径已测 |
| 多人/多 Mod/复杂斜坡/全体型衣服/长期性能 | NOT_RUN |

当前限制：高速自定义配置仍会提高步频；转向的 2D 左右翻面仍是离散切换，未添加新转身贴图。启停混合并非完整足步规划器，复杂斜坡与窄角仍需专项验证。没有用 GIF、单张 FPS 或数学测试替代完整体验验收。

正式用户目录的 save.sv、settings.json、Player.log 前后 SHA-256 一致。最终隔离游戏保留运行供用户体验；运行时 DLL 与下列交付 DLL 哈希一致。

## 交付

动态对比：`runs/v046-heavy2-comparison/walk-before-after.gif`；持续左行片段：`walk-loop.gif`；对应接触帧：`walk-frames.png`。均为实机原速、15fps 采样。对照两版同时持续左行的 4.33–5.13 秒区间，避免撞障碍后的静止片段。

最终包：`artifacts/WayfarerMech-v0.4.6-heavy-candidate.zip`。此前无 heavy 名称包和 heavy 首轮 stage 是过渡/失败候选，不是本次交付。最终 stage 为 `artifacts/wayfarer-v0.4.6-heavy2`。

DLL SHA-256：`035a53ebf888e4d31eb73b983bf87fb59e5cc856b0b45193545f0c7c5eec3a3e`。
ZIP SHA-256：`233f93459f3e6365aa70bc57c328e98b5db52f211651399614b15d8b135a7108`。
