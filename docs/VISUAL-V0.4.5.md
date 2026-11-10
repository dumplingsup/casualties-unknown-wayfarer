# v0.4.5：紧凑鸟人开放舱候选

2026-11-10。分支 `codex/wayfarer-compact-avian`。v0.4.4 仅作技术回退，不作为用户认可的美术方向。

## 设计依据与变化

先实际查看 v044-finalpose 的装配与暗洞站姿，再查看本地 Starbound avianproto 原始参考和鸟人机甲图集。参考的是紧凑主体、骨白/赭色分块与机械挂点关系，不复制原始像素。Starbound 页面入口：https://starbounder.org/Mechs 。

旧长顶棚悬挑、空腔和下方短腿的重量关系不协调。本轮去掉顶棚，把短喙融入低位胸甲；两枚短羽板留在座椅后方。后脊梁—座舱底盘—髋梁连成承重链。近侧钻臂有三角加强板，远侧工具轴前移/下移到有斜撑的侧挂座。减少机身栅格、铆钉与腿部重复关节环，扩展胫甲和脚底实体面积。

完整侧视概念参考 `runs/v045-design/concept.png` 由内置 image_gen 生成；仅作参考，不直接缩小当贴图。实际 PNG 来自仓库可编辑生成器 `tools/art/gen_starbound_style_mech.py`。未导入 Starbound 原始 PNG，也没有把概念图中的角色替换成游戏主角。

真实头、眼、表情、鼻部状态、躯干、衣服与手臂仍由 PilotVisualProxy 读取运行中的玩家；没有改成固定头像。前景脸部窗口静态检查通过。

## 坐标与范围

Champion / sb_avian / sb_avianproto 共用重做的 64×64 双层资产，pivot=(32,34)，PPU=11。头锚点保持 (30,24)，髋部 (24/40,60)，近工具轴 (46,48)，远工具轴约 (50,40)。运行时身体偏移 y=-9.6px，远工具位置改成 (18,-4)px。共享腿的骨长不变，保留已实现的接地求解。

去掉旧 76px 画布中伸到 x=74 的高位长喙，缩短头顶和前方悬挑。碰撞盒保持原物理尺寸，未改移动/伤害/存档格式。背部装饰与可瞄准工具仍可超出盒体；没有据此宣称窄通道完全通过。

## 实机证据与限制

- `runs/v045-visible/shot-script-assembly-settled.png`：明亮装配预览。
- `runs/v045-visible/art-01-idle-right.png` 至 `art-07-descent.png`：真实场景站立、左右行走、喷气与落体。
- `runs/v045-design/before-after.png`：同裁剪框、整数 2 倍最近邻，未调色。
- `runs/v045-design/cave-before-after.png`：暗洞原始光照对照；不同测试世界的背景与照明条件不相同，不能当严格同场景照度对照。
- `runs/v045-compact` 的隐藏窗口输出全黑，因此这轮截图无效，只保留集成日志。

视觉判断：头顶长悬挑和大空腔已消除，驾驶员与底盘关系更直接；低位胸甲和后脊梁可读。正常倍率的暗洞中脸仍较小，双腿旋转后的像素边缘较碎，远侧工具斜撑部分被近侧工具遮挡。不是用户已认可的最终美术。

## 验证分层

| 层 | 本轮结果 |
|---|---|
| STATIC | PASS：Python 19 项，manifest/透明面部窗口检查；姿态数学 60 断言 |
| BUILD | PASS：Mod、Probe locked Release，0 warning/error |
| STAGE | PASS：24 文件，固定 BepInEx 5.4.23.6、CUCoreLib 1.1、游戏 build 26525321 |
| 隔离 | 游戏重定向 11、CUCoreLib 3；unsafe path/prefs/achievement=0 |
| BOOT | PASS：实际加载 0.4.5 和 399 资源；旧有 CUCoreLib Wearable.CreateSprites 警告、Steam 初始化失败单独保留 |
| GAMEPLAY 集成 | PASS：可见测试完成两手、双脚接地 footError=0、缺手标记 2→1→2、左右移动/喷气、97 部件构建、换装、再登机、保存 |
| 真实键鼠 | BLOCKED：computer-use 选中隔离游戏窗口后返回魔兽世界画面；未向错误画面发送输入 |
| SAVE_RELOAD | PASS：v045-final 保存后结束进程；v045-reload 新进程 loadedRun=true，恢复 Champion / drill / cannon / strider / vector、bone，再登机两手两脚通过并完成脚本。结束进程使用 Stop-Process，不计正常退出 UI 验收 |
| 多人/多 Mod | NOT_RUN |
| 多体型/服装、复杂斜坡/窄角、长期性能 | NOT_RUN；本轮不是完整兼容/性能矩阵 |

Probe 新增 CU_PROBE_KEEP_ART_LOADOUT=1，通过真实装配面板 Apply 方法保存当前视觉候选，避免测试结束总是被换成其他机体；测试 DLL 不进玩家包。

## 概念提示词

内置 image_gen；核心完整设计约束：完整右向侧视、约 70×75 原生像素轮廓放大、低饱和赭/骨白/钢灰、紧凑无顶开放舱、可见深色兽人驾驶员与控制杆、低于面部的短喙胸甲、连续后脊梁和髋梁、两条粗壮关节腿与宽脚、座后两片短羽板、近钻远炮各有支撑挂点、少量大色块、无碎噪点、无文字。生成结果仅作为结构草案，真实角色以游戏渲染器为准。

## 交付与正式环境

正式数据目录本轮前后 3 个文件集合及 SHA-256 完全一致，证据 `.local/v045-formal-before.json` / `v045-formal-after.json`。未部署正式游戏。测试 PID 18648、18312、18016、14680 均在核对路径后结束；均先等待对应保存/脚本完成。没有操作用户其他游戏进程。

候选包：`artifacts/WayfarerMech-v0.4.5-candidate.zip`，仅 DLL + README.txt。
DLL SHA-256：`dcfe3149f9541c7c6df5dc22c5640fe4dfb270c8a7f296bd85c62baa44ef562c`。
ZIP SHA-256：`fdf4795a775998b5ef91633ecd8db5d26a03b762f500b281e34fbf73df894200`。
锁和 stage：`artifacts/wayfarer-v0.4.5.lock.json`、`artifacts/wayfarer-v0.4.5`；运行时复制 DLL 与锁中哈希一致。旧 v0.4.4 包保留用于回退。

## 远端推送后真实窗口输入复测（2026-11-10）

开发提交 adfe870 已推送 origin/codex/wayfarer-compact-avian，未合并 main。
`runs/v045-remote-play`，隔离游戏 PID 4484。fakeinput=False，先由 fixture 读档、布置平地和登机；之后由 computer-use/Windows SendInput 操作真实窗口，不使用 ProbeInput。

这轮桌面捕获正确显示 CasualtiesUnknown，先前捕获错位阻塞未再出现。实际输入 D 后观察到移动数据；F 下机得到 PILOT_EXIT；P 打开装配；鼠标点击 Apply 得到 ASSEMBLY_APPLY_BUTTON 和 ASSEMBLY_APPLIED；F 再次登机；右键机炮 shots 从 0 到 1。最后 Esc，窗口显示暂停菜单，观测 paused=true、piloting=true。这是限定路径的真实窗口输入通过，不是全部玩法或人工手持键鼠验收；本轮未单独验证持续喷气/长时间钻掘。

正式数据目录的 3 个文件与哈希在此次检查前后不变，记录 `.local/v045-remote-play-before.json` / `v045-remote-play-after.json`。保留该测试实例暂停供用户继续操作；本轮未宣称正常退出或又一次保存读档。
