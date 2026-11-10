# 本次交付验证

2026-11-10，目标 Casualties: Unknown；没有修改原 Mod 工程、游戏安装或用户数据。

| 项目 | 结果 | 证据 |
|---|---|---|
| CLI 自动测试 | PASS，13 项 | `python -m unittest discover -s tests -v` |
| 工作区 skill | PASS | skill-creator 的 `quick_validate.py` |
| 本机探测 | PASS | build 26525321；主程序集 hash 与既有基线一致；BepInEx 未安装 |
| 实际 profile lock | PASS | `artifacts/titan-v1.lock.json` |
| 实际组合 stage | PASS，24 文件 | `artifacts/titan-v1/payload` |
| stage 完整性 | PASS | `python tools/cumod.py verify artifacts/titan-v1` |
| 原 Titan 重新编译 | NOT_RUN | 使用已有 DLL；没有声称构建源与该 DLL 本次重新对应 |
| 安装/启动/真实游玩/读档 | NOT_RUN | 本次交付工具与规范；未准备隔离数据环境、未执行游戏 |
| 部署事务和测试桥 | NOT_IMPLEMENTED | 设计契约在 MANAGER / RUNTIME-TESTING；不属于已有 CLI 能力 |

测试覆盖依赖顺序、依赖缺失、版本不符、依赖循环、声明冲突、大小写与文件/目录冲突、不安全目标路径、ZIP 成员、源文件漂移、游戏指纹漂移、产物缺失/额外/修改、未完成输出/禁止覆盖、锁文件目标路径篡改。

限制：未自动核对 PE 元数据、注册表冲突或 Harmony 语义兼容；哈希不是签名。该本地 bundle 含 loader 与第三方库，只作为本机组合样例，不代表已完成公开分发许可核查。
