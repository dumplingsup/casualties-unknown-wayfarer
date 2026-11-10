---
name: mod-workflow
description: Develop, assemble, and validate Casualties Unknown mods using BepInEx and CUCoreLib, with reproducible profiles, compatibility checks, and evidence from actual gameplay and save reloads.
---

# Casualties: Unknown Mod workflow

此 skill 随工作区分发，由根目录 AGENTS.md 路由。无需全局安装。

1. 找到包含 `tools/cumod.py` 的工作区根。先读 `docs/WORKFLOW.md`；接手已有项目同时读取其工程和交接记录。不要沿用另一游戏的测试工具。
2. 用 `cumod doctor` 读取当前游戏版本和程序集哈希。将游戏、CUCoreLib、BepInEx 与构建工具锁定；构建前检查 MSBuild 自定义 Target 是否会复制到游戏。
3. 根据任务选择注册表扩展、独立行为组件或 Harmony 补丁。通用约束见流程文档；美术、多人等专用内容按需读取项目文档。不要为每种 Mod 强制引入 CUCoreLib。
4. 修改后，先构建与静态检查，再生成 profile lock 和独立 stage，验证交付文件集合与哈希。管理器命令见 `README.md`。失败时定位依赖或构建层，不把失败包装成已安装。
5. 需要实机验证时读 `docs/RUNTIME-TESTING.md`。先证实存档隔离，再在真实游戏中执行正常交互路径、异常条件和重启读档。现有工具不足时使用可用 UI 自动化；需要新测试桥时按文档契约实现，不能假设接口已存在。
6. 每次运行保存输入指纹、步骤结果、截图/状态/日志和未覆盖项。无能力、无环境或未执行的步骤记 BLOCKED/NOT_RUN，不记 PASS。更新当前项目交接文档。

扩展管理器时读 `docs/MANAGER.md`，保持 CLI、manifest 和测试桥分层；当前只实现离线组合与验证，部署事务和桥接协议是明确的后续实现边界。
