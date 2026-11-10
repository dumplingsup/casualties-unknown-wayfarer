# v0.4.0 纳管审查（2026-11-10）

结论：适合建立私有开发仓库，尚不适合宣称完整移植或稳定发布。
本次复核源码、现有截图/报告、构建和纯测试；没有重新启动游戏实测。

## 已确认

- `dotnet build ... -c Release -p:RestoreLockedMode=true`：0 warning / 0 error。
- Python unittest：16/16；DrillShapeTests：20 个断言通过。
- `cumod doctor`：build 26525321，Assembly-CSharp SHA-256
  `de2462a80d1a6d0a95a3d8032589a1b30e784853cb6bbd3951a52eeb67fc7335`。
- `cumod verify artifacts/wayfarer-v0.4.0-final2`：24 文件 PASS。
- 重新编译 DLL SHA-256 与交付报告一致：
  `32629898F09C6C06D66BD42CD8FDCB9E8342B40D8BCC66AC3885FCFE74C38550`。
- 原创 PNG 生成器与部件目录已存在；原版解包图和预览不进入仓库。

实机加载、玩法、读档证据来自已有 v0.4 报告和本机 runs，不是本轮复测。
物理键鼠、完整死亡流程、友军 NPC、多 Mod 和多人矩阵仍按原报告保留未验证状态。

## 需要修复/补测

1. **近战审查更正（2026-11-10）**：上轮因输出截断漏看了 `MeleeArm` 后半段，
   错误报告缺少人形伤害。完整源码已有 `hit.body` / `IsFriendly` / `DamageBody` 分支。
   撤回此代码缺陷结论；敌对人形与友军的实机命中矩阵仍待验证。
2. **未来版本存档保护与注释不一致（P2）**：`MechLoadout.FromJson` 对较新 schema
   返回默认配置；`Restore` 仅保存该默认值，`Capture` 无条件重新输出 schema 1。
   因此下一次保存会覆盖原始未知字段，不符合注释“仅显式应用后才覆盖”的承诺。
   当前 schema 1 正常读档不受该路径影响；需要保留原始 payload 或明确拒绝降级保存，
   并测试“载入较新 schema → 不操作装配 → 保存”的数据保真。
3. **自动化美术测试覆盖偏旧**：`tests/test_art_rig.py` 仍主要验证
   `gen_wayfarer_art` 与旧 rig，16 项通过不代表 v0.4 全目录/新机体 rig 都有纯测试。
   现有 97 部件构建是历史实机审计。后续补新 manifest/部件 ID/锚点一致性测试。
4. **特殊臂仍是行为映射**：护盾、无人机、地雷等没有各自机制，不能只凭条目名宣称实现。
   优先做差异最大的行为，而不是继续堆目录数量。

## 视觉判断

查看 `tools/art/preview/starbound_style_bodies.png` 与
`runs/v040-catalog5/shot-script-assembly.png`：开放舱结构比早期箱体更有辨识度；
但大多数机体共用鸟喙、羽冠和圆腹轮廓，主题变化主要是配色/装饰。
这适合作为鸟人系家族原型，不足以代表所有 Starbound 种族的机体设计。
装配截图中标题说明与部件标题重叠，背景过暗，实时机体预览被右侧文字/配色按钮压住。
下一轮优先给预览一个独立可读区域，并分别设计鸟人、人类、猿人轮廓；不要先扩数量。

## 纳管决策

保留原交付源码作为可追溯基线，本轮不悄悄改动 gameplay DLL。
上述问题登记为后续修复项；仓库初始化与 CI 不等于这些问题已经解决。
