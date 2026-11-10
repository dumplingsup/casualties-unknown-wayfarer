# CU Mod Kit：未知伤亡 Mod 工作台

**最新 v0.4.3：** 参照概念图重做鸟首顶棚驾驶舱，加入座椅/护栏/操作台，调整上下工具挂点并加宽钢钻。
[结构与验证记录](docs/VISUAL-V0.4.3.md)。

**最新视觉迭代 v0.4.2：** 收窄机身、简化鸟喙羽片、重画承重腿、调整驾驶员坐姿与 HUD。
范围和实机验证见 [视觉迭代记录](docs/VISUAL-V0.4.2.md)。

**最新迭代 v0.4.1：** 修复未知存档字段保真，增加明亮的实时装配预览，补充当前资源清单与存档测试。
验证边界和剩余任务见 [v0.4.1 测试报告](docs/WAYFARER-V0.4.1-TEST-REPORT.md)。

代码管理入口：[构建与协作指南](docs/REPOSITORY.md)；
[v0.4 纳管审查与待办](docs/WAYFARER-V0.4-REVIEW.md)。当前为开发候选基线。
源码、原创资源和工具随仓库保存；下文提到的 artifacts/runs/versions 与本机 profile 不随 clone 分发。

面向《Casualties: Unknown》，把一次性的 Mod 开发整理成可重复的“环境指纹 → 构建 → 组合锁定 → 验证 → 实机验收 → 交付”。2026-11-10 初版。

本次交付有两部分：**已经可运行的轻量离线 Mod 组合管理器**，以及 **agent 可执行的完整开发、部署与实机测试规范**。没有 GUI；部署事务、游戏内测试桥还没有实现，也不声称已经自动玩过游戏。

**2026-11-10 更新（v0.4.0）：** 完整 Starbound modular mech 目录移植候选已实现：本机可取
的 **29 机体 / 27 臂 / 8 腿 / 8 推进器 / 14 喇叭** 全部成为可选条目（加原创起点共
**31/30/10/10/16**），每项有独立 ID、属性、图标和存档；全目录 **97 项逐一在隔离游戏内
构建成功**。开放舱仍显示真实主角头/眼/表情；装配台分页；新增近战行为；原版战斗/顶碰/
高空存档/读档/生命周期路径均有本轮 v0.4 证据。候选包在
`artifacts/WayfarerMech-v0.4.0-candidate.zip`，完整清单/哈希见
`artifacts/wayfarer-v0.4.0-delivery.json`。逐项结果与 BLOCKED/NOT_RUN 见
[docs/WAYFARER-V0.4-TEST-REPORT.md](docs/WAYFARER-V0.4-TEST-REPORT.md)；资产/许可边界见
[docs/ART-ASSETS.md](docs/ART-ASSETS.md)。物理键鼠验收仍被锁定桌面阻塞（OS 窗口消息路径
已测移动/喷气/装配 Apply），未上传 Workshop。

**历史：** v0.3 的 2 机体开放舱原型见 `docs/WAYFARER-V0.3-TEST-REPORT.md`；v0.2 快速侦察
机体见 `docs/WAYFARER-V0.2-TEST-REPORT.md`；v0.1 回退在 `versions/wayfarer-0.1.0/`。

## 从哪里开始

| 文件 | 用途 |
|---|---|
| [Starbound 风格机甲移植执行文档](docs/STARBOUND-MECH-PORT-EXECUTION.md) | 鸟人开放舱、真实角色表情、模块换装、双臂与存档安全 |
| [Wayfarer v0.4 测试报告](docs/WAYFARER-V0.4-TEST-REPORT.md) | 完整 29 机体/27 臂目录、97 部件构建、v0.4 实机证据、BLOCKED/NOT_RUN |
| [Wayfarer v0.4 美术与来源](docs/ART-ASSETS.md) | 当前原创资产/目录生成器、pivot/PPU、Starbound 参考与许可边界 |
| [Wayfarer v0.3 测试报告](docs/WAYFARER-V0.3-TEST-REPORT.md) | 历史 2 机体开放舱原型证据 |
| [Wayfarer v0.2.1 结构审查](docs/WAYFARER-V0.2.1-REVIEW.md) | 历史结构优化、装配预览修复、候选版验证状态及后续风险 |
| [Wayfarer v0.2 执行文档](docs/WAYFARER-V0.2-EXECUTION.md) | 当前可驾驶机甲的喷气、钻头强化、美术重做方案与接手验收；v0.2.0 已实现，结果与未验证项见[测试报告](docs/WAYFARER-V0.2-TEST-REPORT.md) |
| [当前流程分析与开发流程](docs/WORKFLOW.md) | 为什么这样改、每次开发怎么做、兼容与发布标准 |
| [管理器规范](docs/MANAGER.md) | profile 格式、现有能力、安装回滚与扩展接口设计 |
| [真实游玩验收](docs/RUNTIME-TESTING.md) | 隔离存档、自动化边界、通用测试矩阵、外骨骼具体案例 |
| [工作区 skill](.agents/skills/mod-workflow/SKILL.md) | 给 coding agent 的入口；根 AGENTS.md 会引导读取 |
| [测试案例模板](profiles/gameplay.example.json) | 每个 Mod 按功能填写可观察的动作与断言 |
| [来源与本机基线](docs/BASELINE.md) | 已核查的信息及其适用范围 |

## 使用管理器

Python 3.10+，仅标准库。在此目录打开 PowerShell：

```powershell
python tools/cumod.py doctor "D:\SteamLibrary\steamapps\common\Casualties Unknown"
python tools/cumod.py lock profiles/titan.local.json --out artifacts/titan-v1.lock.json
python tools/cumod.py stage artifacts/titan-v1.lock.json --out artifacts/titan-v1
python tools/cumod.py verify artifacts/titan-v1
python -m unittest discover -s tests -v
```

`profiles/titan.local.json` 是本机生成的实际示例，引用现有 loader ZIP、CUCoreLib DLL 和 Titan DLL；它不是可搬家后原样使用的配置。通用字段说明在管理器规范。换电脑后改 source 路径并重新核对哈希；不要无条件接受新哈希。

本次已生成 `artifacts/titan-v1`，可以直接执行 verify；再次 lock/stage 请改用新的输出名，如 `titan-v2`。需重新生成本机示例时使用 `python tools/make_titan_profile.py --help` 查看参数。该辅助脚本专用于当前 Titan 0.1.0 / CUCoreLib 1.1 基线，版本变化要更新声明并核对程序集元数据。

启用/停用：编辑 profile 的 `enabled`，再生成新的 lock/stage。缺依赖、版本不符、循环依赖、文件覆盖冲突会拒绝组合。回到旧组合：选择之前通过验证的 stage；真实安装层的回滚需遵循事务规范。

输出的 `payload/` 是**相对游戏根目录的文件布局**；lock 保存每个文件的所有者与 SHA-256。没有执行下载、安装、启动、卸载、触碰存档或发布。不要对正在使用的游戏目录盲目覆盖 payload。

默认拒绝覆盖已有 lock/stage。版本变化用新的输出目录；未完成目录没有有效 COMPLETE 标记。`verify` 校验的是文件完整性，不是可信签名、API 兼容或游戏测试。

## 给 agent 的一句话

> 按这个工作区的 mod-workflow skill，为 Casualties: Unknown 开发【功能】。使用独立 profile，先检查游戏和依赖指纹；按对应 gameplay cases 完成真实交互与重启读档验证。把构建、文件验证、实机验证的结果分开报告；不能执行的项目保留为 NOT_RUN/BLOCKED。

当前外骨骼项目无需整体重写。优先移除构建自动部署、固定依赖版本、采用此处 profile 和测试案例，再按需求实现测试桥。
