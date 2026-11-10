# Wayfarer 视觉重做交接

## 接手进展：v0.4.5

已在 `codex/wayfarer-compact-avian` 实现紧凑开放舱重做；当前报告以 [VISUAL-V0.4.5.md](VISUAL-V0.4.5.md) 为准。旧版为回退，不代表已认可。

新图：`runs/v045-design/before-after.png`、`cave-before-after.png`。实际可见测试：`runs/v045-visible`、`runs/v045-final`，专用重启读档：`runs/v045-reload`。`v045-compact` 隐藏窗口截图全黑，不作视觉证据。候选包 `artifacts/WayfarerMech-v0.4.5-candidate.zip`。

保留真实面部与表情代理；未改正式安装、邻近 Titan 或公开发布。美术仍未经过用户认可，腿部旋转像素边缘、远侧工具遮挡仍可优化。以下为接手前历史。


更新时间：2026-11-10。工作区：`D:\testGround\furry`。

## 最重要的状态

**用户没有认可 v0.4.4 的美术。最新反馈是「还是有点怪和不合理」。**
上一会话称它适合作为视觉基线，只是 agent 的判断，已被用户反馈否定。它可以作为可回退的技术基线，不能当成已验收的设计方向。
本次只记录交接，没有继续改图、代码、部署或启动游戏。

用户希望：符合《Casualties: Unknown》的像素画风，结构合理的可驾驶采矿/战斗机甲；开放驾驶舱能看见主角本体、脸部和表情。喜欢 Starbound 机甲，特别鸟人族。参考入口：https://starbounder.org/Mechs 。用户本地曾准备 Starbound 素材及大型像素素材库；具体路径请查已有美术文档，别猜或重复下载。

## 下一位 agent 应先做什么

1. 先读 `AGENTS.md`、`.agents/skills/mod-workflow/SKILL.md`，再读 `docs/WORKFLOW.md`、`docs/RUNTIME-TESTING.md`、`docs/ART-ASSETS.md` 和 `docs/VISUAL-V0.4.4.md`。
2. **实际打开下面的截图**，对照游戏原生人物、场景与 Starbound 鸟人机甲参考。不能只读代码和日志就评价美术成功。
3. 重新判断整体比例、轮廓、驾驶空间、承重关系、工具挂载和运动逻辑，先做清晰的完整侧视方案，再落地拆层与动画。允许舍弃当前鸟首顶棚形状，不必维护上一 agent 的设计结论。
4. 用户允许自主迭代，不必反复确认常规实现。但视觉成功没有自动判定标准；需给出可看懂的前后对比，说明仍存在的问题，避免以测试通过代替外观验收。

### 待核实的视觉问题（分析方向，不是用户逐项提出的问题）

- 鸟首长顶棚悬挑明显，头顶空腔较大，可能更像装饰帽檐，缺少与座舱的整体关系。
- 上身、较细碎的关节和较小脚掌重量关系是否可信？承重链能否一眼读出？
- 角色、座椅、手臂和操纵杆在正常游戏倍率下是否可辨，还是堆叠在一起？手数正确不等于姿态自然。
- 钻头、机炮和连接臂是否各自有明确挂点、运动空间和受力结构？两臂瞄准时是否互相遮挡？
- 程序生成的碎小高光和轮廓线是否过密？不要继续只靠改颜色、加面板、加羽片解决整体问题。
- 原生洞穴很暗；需要同时看明亮预览与实际环境，不靠强制全亮掩盖适配问题。
- 视觉悬挑与现有碰撞盒不完全一致；若重做大幅改变轮廓，需一并评估通行与操作反馈。

## 当前代码与资产

私有仓库：https://github.com/dumplingsup/casualties-unknown-wayfarer 。分支 `main`。
交接前 HEAD：`09fa7d6`，v0.4.4；代码工作树干净。Source checks CI 已成功。
此前 v0.4.3：`c8ea3ce`。不要自动修改邻近 `../casualties-mecha-mod`（Titan 项目）。

| 文件 | 用途 |
|---|---|
| `tools/art/gen_starbound_style_mech.py` | 现有原创像素资产生成器；重点 `champion_cockpit`、`leg_layers` |
| `src/WayfarerMech/Assets/` | 内嵌 PNG，生成资源已纳管；只改 PNG 可能被生成器覆盖 |
| `src/WayfarerMech/MechVisual.cs` | 分层、挂点、工具、腿部姿态、脚底地面探测 |
| `src/WayfarerMech/PilotVisualProxy.cs` | 实际角色头/眼/躯干/服装显示；`AttachSeatedHands` 创建手臂渲染副本 |
| `src/WayfarerMech/TwoBonePose.cs` | 不依赖 Unity 的两骨骼求解 |
| `tools/MechProbe/Probe.cs` | 隔离测试、自动输入、截图及状态观测；不进玩家包 |
| `tools/launch_mech_test.ps1` | 测试启动器；运行前读参数和路径 |
| `tests/PoseTests/` | 姿态数学测试；不能证明美术合理 |

v0.4.3 为 Champion / `sb_avian` / `sb_avianproto` 添加鸟首顶棚、座椅、操作台、前护栏，改变远侧工具挂点，加宽钢钻。
v0.4.4 添加真实手臂/手与服装渲染副本，缺肢隐藏；视觉脚底接地、步频调整、腿甲扩宽和暖色配色。上述内容均可重做。
手部修改仅影响显示副本，不移动真实角色肢体；脚底 IK 不改变碰撞或角色运动。
目录有 97 个可选部件，但战斗功能仍是若干共用行为族，**不能宣称完整忠实移植所有 Starbound 特殊功能**。

## 必看的本机图像与证据

- 当前明亮装配预览：`runs/v044-finalpose/shot-script-assembly-settled.png`
- 当前站姿：`runs/v044-finalpose/art-01-idle-right.png`
- 当前向右/左行走：`runs/v044-finalpose/art-02-walk-right.png`、`art-03-walk-left.png`
- 喷气等姿态：同目录 `art-04-jet-left.png` 至 `art-07-descent.png`；按实际截图判断状态，不单凭文件名。
- 前一版：`runs/v043-final/shot-script-assembly.png`
- 更早结构：`runs/v042-finalvisual/shot-script-assembly.png`
- 最新重启读档：`runs/v044-reload/Player.log`、`observations.jsonl`
- 详细测试层与限制：`docs/VISUAL-V0.4.4.md`；历史见 v0.4.2/v0.4.3 记录。

历史生成概念提示词：`像素风双足采矿机甲，鸟人风格开放驾驶舱，驾驶员面部可见，钻头和机炮，完整侧视图。`
该概念图在旧会话中显示，本交接未确认它有可用本机文件；不能假装新会话已看到。需要时重新生成参考，图像生成应读 imagegen skill。概念图不是可直接缩小使用的游戏动画图集。

## 测试与环境边界

- 游戏：`D:\SteamLibrary\steamapps\common\Casualties Unknown`，Steam build 26525321、Unity 2022.3.62f3、Mono/net48。
- 必要依赖：BepInEx 5.4.23.6、CUCoreLib 1.1。现有构建参数、锁文件和路径见 `docs/REPOSITORY.md` 与本机 profile，不要另装未知版本。
- 仅测试 `.local/cu-test/game`，用户数据 `.local/cu-test/userdata`。隔离校验游戏 11、CUCoreLib 3 处路径；unsafe path/prefs/achievement=0。
- 正式目录 `C:\Users\42173\AppData\LocalLow\Orsoniks\CasualtiesUnknown`，本轮文件集合及 SHA-256 未变；基线 `.local/v044-formal-before.json`。正式 save.sv 哈希 `aad02cefd5c55dc68345fab7608e39a2dfa6a08b233453910b9a19a81a501fab`。下一轮仍要重新采集，不能把旧哈希当新证据。
- v0.4.4 Mod/Probe 编译通过；Python 19 项、姿态 60 断言、钻掘 20 项、存档 6 项通过。
- 实机自动化完成两手显示、平地双脚接地、缺手标记 2→1→2、97 部件构建、换装、再登机、保存；退出进程重启后恢复 Dominion 配置并再次完成脚本。
- **不等同真实键鼠验收**：使用 ProbeInput/游戏方法。桌面工具虽然定位到 CasualtiesUnknown 窗口，截图却返回不相干游戏内容，本轮未据此输入。新会话需重新验证工具，不把历史上锁桌面的推测当事实。
- 多体型/服装矩阵、复杂斜坡/窄角、多人、多 Mod、完整长期性能对照未通过完整验收；不要补写 PASS。
- 测试进程 4568 与 24996 已关闭。后续仅管理自己启动且路径核对过的 PID，不能全局杀同名进程。
- 既有启动环境变量：`CU_PROBE_AUTOSTART=1`、`CU_PROBE_AUTO=1`、`CU_PROBE_FAKEINPUT=1`、`CU_PROBE_AUTOSHOT=1`；视觉流程可加 `CU_PROBE_ART=1`、`CU_PROBE_CATALOG=1`、`CU_PROBE_HANDAUDIT=1`。读档用 `CU_PROBE_LOADSAVE=1`。先检查 Probe 最新代码再使用。
- 示例启动：`./tools/launch_mech_test.ps1 -Run <唯一运行名> -Visible`。自动流程会更换测试装配并保存；不要在正式数据上运行。

## 交付与继续工作的约束

本机候选包 `artifacts/WayfarerMech-v0.4.4-candidate.zip`，仅 DLL+README；stage `artifacts/wayfarer-v0.4.4` 校验 24 文件通过。
DLL SHA-256：`967768762a6640b85885a8963d7c5c8ecfa4099dd5388c13e3f438a2470f13f9`。
包 SHA-256：`52efa6b0f2a1e5c65c06e4a2642af161cb9bb2b8081e6929a8c9e00a2c49d15c`。
源码/原创 PNG/规范可入私有 Git；`runs`、`artifacts`、`.local`、游戏文件、外部原始素材与凭据不提交。
已有授权覆盖开发、隔离测试和私有代码管理，不等于公开发布、Workshop 上传或正式游戏部署。
新分支默认 `codex/`。先解决视觉结构，再运行与改动相称的测试；保留回退，最终给真实截图和明确未测项。
