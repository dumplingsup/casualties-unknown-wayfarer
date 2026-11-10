# Wayfarer v0.4.1 迭代验收（2026-11-10）

本轮修复存档保真、改进装配面板，并补当前模块资源检查。没有重绘机体，也没有新增
导弹/护盾/无人机机制。近战源码原本有人形伤害分支，上轮审查误报已更正。

## 实现

- 读取配置时保留完整 payload 和 provider envelope version。较新版本、缺失部件和
  畸形数据可使用运行时默认配置，但不因再次保存而覆盖原始记录。
  玩家主动应用装配才清除保留记录，输出当前 schema。Capture 返回独立深拷贝。
- CUCoreLib 1.1 已核查为逐角色同步调用 Capture → GetVersion；provider 返回该次
  Capture 对应的原始版本，不把未来记录误标成版本 1。依赖版本改变时必须复核此契约。
- 装配面板消除标题重叠，增加独立明亮预览区，绘制真实机甲当前姿态及驾驶者精灵；
  不克隆角色、修改游戏相机或改动角色渲染层。小窗口按整体比例缩放。
- 增加所有 399 张 PNG 的尺寸/PPU/pivot/清单完整性检查、31 机体的前后双层/四色完整性检查。
- TestIsolation 增加只读 `--verify`，可以确认游戏/库仍重定向数据路径且禁用正式偏好和成就写入。

## 验证结果与边界

| 层 | 结果 | 本轮证据 |
|---|---|---|
| BUILD | PASS | Mod + MechProbe Release，locked restore，0 warning / 0 error |
| PURE TESTS | PASS | Python 18/18；钻掘 20 断言；存档快照 6 断言 |
| ISOLATION | PASS | 游戏重定向 11 处，CUCoreLib 3 处；unsafePaths/Prefs/Achievements 均 0 |
| STAGE | PASS | `artifacts/wayfarer-v0.4.1`，24 文件；无测试探针 |
| LOAD / AUTOMATED GAMEPLAY | PASS | `runs/v041-preview2`；召唤、登机、喷气、钻/炮、下机、装配、再登机、保存 |
| CATALOG | PASS | 同轮 `PROBE_CATALOG_BUILD built=97 failed=0` |
| SAVE PROVIDER | PASS | 同轮 `PROBE_SAVE_RECORD_PASS`：未来 payload/envelope 保留、主动应用替换、畸形记录保留 |
| SAVE RELOAD | PASS | `runs/v041-finalreload`：进程重启后恢复 Dominion / Solus Katana / Gatling / Arc / Zed / steel，再召唤与登机 |
| VISUAL | PASS / limited | `runs/v041-preview2/shot-script-assembly.png`；1280×720 的 Champion 独立预览/真实角色/按钮布局已目视检查 |
| PHYSICAL INPUT | BLOCKED | LogonUI 仍存在；本轮使用 ProbeInput 与游戏方法自动化，不宣称真实物理键鼠通过 |
| COMPATIBILITY | NOT_RUN | 友军/敌对人形命中矩阵、多人、其他 Mod、完整死亡/重生未补测 |
| PERFORMANCE | NOT_RUN | 本轮没有完整 60 秒压力或多帧率对照，不继承旧性能结论 |

未来/畸形数据通过真实游戏中的 provider Restore/Capture 回环验证；完整文件保存重启测试
使用正常 schema 1 的角色换装。尚未对“未来 schema 文件”做跨进程往返，不能混称已通过。
装配截图验证不等于新坐标下按钮点击验证；分页/Apply/Cancel 的物理交互仍待解锁后验收。
UI 小分辨率、左右朝向/全部角色外形/全配色截图矩阵尚未完成。

## 调试过程

- 首轮隐藏窗口 `v041-regression1` 截图为黑，只有状态/日志可用，未当作视觉通过。
- `v041-reload1` 显示新预览区空白，去除与 GUI 变换冲突的嵌套裁剪后，
  `v041-preview2` 截图已显示完整机甲；保留失败证据，不覆盖旧运行目录。
- Probe 直接读取 Application.persistentDataPath 仍显示 Unity 默认路径；实际文件读写
  已在游戏及 CUCoreLib 调用点重定向。不能仅凭这个默认路径日志判断隔离失败或成功。

## 交付

候选 DLL SHA-256：`7b50a6b513cba17ceac471a62f9ca446b61737551c82c9c6aa961ebd0580089e`。
玩家包：`artifacts/WayfarerMech-v0.4.1-candidate.zip`，仅 DLL 与 README。
正式安装和邻近 Titan 项目未修改，正式存档/设置文件逐项核对哈希。
测试证据位于本机 runs；源码、测试及本报告纳入私有 GitHub 仓库，未发布 Workshop。

后续优先：解锁后的装配鼠标交互、未来 schema 文件往返、人形战斗矩阵；然后再分化种族机体轮廓。
