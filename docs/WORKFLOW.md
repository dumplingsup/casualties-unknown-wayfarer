# 开发流程与现状分析

## 1. 本次分析范围

分析对象是邻近的 `casualties-mecha-mod`：README、CLAUDE.md、HANDOFF、game-reference、TitanExosuit.csproj、vars.targets、Plugin.cs、TitanExosuitScript.cs、status/package 脚本，以及本地游戏程序集指纹、CUCoreLib API 文档。没有更改原项目、游戏或用户存档。

现有流程已经具备有价值的基础：逆向依据、稳定插件 GUID、CUCoreLib 内容注册、独立行为组件、内嵌资源、打包哈希、明确“尚未实机验证”的交接记录。问题主要在可重复性和验证闭环，不是必须推倒重做。

| 具体发现 | 影响 | 建议改法 |
|---|---|---|
| 工程包含 AfterBuild 复制 Target，BepInEx 目录一旦存在就向游戏复制 | 同一个 build 命令的副作用随环境变化 | 构建只写构建输出；显式 stage/deploy；旧工程临时用 `-p:RequiredFilesToCopy=` 禁用该 Target，先检查 Target 条件仍匹配 |
| BepInEx.Core `5.*`、Analyzers `1.*`、PluginInfoProps `2.*` | 重新 restore 可能得到不同依赖 | 从已验证 assets/lock 取精确版本，提交 packages.lock.json，使用 locked restore；不能凭空挑“最新” |
| vars.targets 和脚本硬编码本机路径 | 换机器难复现 | source-controlled 构建配置 + gitignored 本机路径；对游戏 DLL、CUCoreLib 和 loader 建指纹 |
| package 脚本接受手输版本，读取现成 DLL | 包名可能与 DLL/源码不一致 | 构建产物写 build-info：源码 revision/dirty、程序集版本、构建参数、哈希；打包核对一致性 |
| 包含 loader ZIP、库、源码和参考图 | 开发包方便，但升级和授权边界混杂 | 日常发布为 Mod-only 包；依赖单列；开发源码/诊断包分开。整合包仅在依赖分发许可和版本组合已确认时生成 |
| 编译和静态资料充分，但没有游戏侧安装/实测 | 尚不能证明注册、视觉、行为、存读档可用 | 分层门禁，必须保留 NOT_RUN；见实机文档 |
| 跳跃、耗电在每件物品的 Update 中发生 | 需要验证场景、暂停、穿戴对象和帧率差异 | 测无电、未穿戴、NPC、菜单、重复进入场景、30/60/高帧率。此处是测试风险，不是已证实 bug |
| 存档由用户目录持有 | 游戏副本仍可能读写正式存档 | 实测确认数据路径隔离；未确认前不新建/保存测试世界 |

## 2. 结构原则

保持三层：**Mod 功能层、工具管理层、测试适配层**。

- Mod 功能层只依赖实际需要的游戏 API/库。内容优先用 CUCoreLib 已有注册能力；纯语言或音乐不必强行引入插件；库不支持的机制才局部 Harmony 补丁。
- 管理层处理文件、版本、依赖、组合、部署记录与证据目录，不理解“盔甲伤害公式”。这样物品、液体、状态、UI、玩法、汉化都可复用。
- 测试层暴露少量状态和动作，与 Mod 功能分离；测试 DLL 不进入面向玩家的包。正常玩法必须脱离测试层仍然工作。

推荐新项目布局，现有工程只迁移需要的部分：

```text
Project/
  AGENTS.md
  src/ModName/             # C#、嵌入资源、本地化
  tests/                  # 纯逻辑、边界、迁移测试
  profiles/               # 基础/开发/兼容组合；本机路径不提交
  cases/                  # 真实玩法验收案例
  docs/                   # 简报、兼容性、交接、美术规范
  tools/                  # 可复用工具或固定版本工具依赖
  artifacts/              # 不可变的 lock、stage、发布包，不进源码
  runs/<run-id>/          # 每轮环境/日志/截图/结果，不进源码
```

## 3. 每轮开发的执行顺序

### A. 先定义可验收功能

一页简报足够：触发入口、玩家看到什么、核心状态变化、异常情况、持久化字段、依赖、支持游戏 build、是否支持多人。至少有一条正常玩法测试、一条边界测试；涉及持久化就有重启读档测试。将物品/配方/语言 key 和 GUID 视为存档契约。

### B. 环境探测和输入锁定

运行 doctor；检查实际 exe、Mono/Managed 布局和程序集指纹，不把文件夹名当版本。记录 Steam buildid、Assembly-CSharp、UnityPlayer、必要 Unity 模块、CUCoreLib、BepInEx 包、.NET SDK 和 NuGet lock。profile 支持多个 fingerprint，默认示例仅将主游戏程序集作为硬门禁；发布前补齐相关程序集与工具版本。

游戏更新或依赖哈希变化：旧测试证据失效为“旧基线通过”。重新核查受影响类型/方法签名和存档布局；运行基本加载与受影响玩法回归。不要自动覆盖游戏基线或宣称未知 build 兼容。

### C. 小步实现

注册、行为、配置、存档迁移、兼容补丁各自有清晰边界，不要求机械拆成大量工程。

- 注册尽量幂等；进入世界/切换语言/读档不能重复添加内容或事件订阅。
- Harmony ID 使用自己的 GUID；尽量小范围 Postfix/Prefix，只有需要改变流程时才跳过原方法。Transpiler 对方法变化敏感，必须有签名/匹配数检查与失败说明。
- 不全局 UnpatchAll；仅撤销自己的 patch。读取其他 Mod 存在性后再加载可选集成，基础路径不强制引用可选程序集。
- 不复制分发游戏程序集；避免把另一份 Harmony/Unity/BepInEx 核心 DLL 随单个 Mod 偷带进 plugins。发布前核对最终文件清单。
- 主线程才访问 Unity 对象；对场景切换、销毁对象、无玩家、菜单状态做短路。高频逻辑避免反射和全场扫描，缓存需随生命周期清理。
- 纯数值计算从 Unity 行为里抽出才有单元测试价值；不要为了“测试数量”镜像实现。
- 配置要有默认值、范围限制和版本；不把生产存档路径硬编码进 Mod。

### D. 存档兼容

新增持久数据有 schemaVersion、默认值、前向/向后兼容策略。针对当前 CUCoreLib DLL 核对 save provider API；本地文档描述了随原版 save.sv 保存的 provider，不能据此假设任意 MonoBehaviour 字段自动持久化。

改字段/类型/物品 ID 时给迁移，或明确声明破坏性版本且保留旧版本与存档备份。测试 N-1 → N；若不支持降级须明确。禁用 Mod 后含该 Mod 物品的存档不保证可读，管理器不能以“DLL 已删除”宣称可安全卸载。

### E. 构建、检查、stage

固定 SDK 与 NuGet 版本，先 restore locked，再 Release build。新工程的 build 不部署。旧 Titan 工程可在验证 Target 后执行：

```powershell
dotnet build ../casualties-mecha-mod/TitanExosuit/TitanExosuit.csproj -c Release -p:RequiredFilesToCopy=
```

这条命令只解决当前已观察到的 Target，不能替代审查未来新增脚本。若已有 packages.lock.json，再使用 `dotnet restore --locked-mode`；初次生成锁文件需要核对解析版本，不直接宣称已锁定。

静态检查按内容选：程序集引用/版本、GUID 重复、嵌入资源名与引用匹配、locale JSON 和占位符、物品/配方 ID、依赖是否声明、贴图 PPU/pivot/过滤方式。跨 Mod 的语义冲突不靠文件哈希就能识别，留给运行时兼容验证。

生成 profile lock → stage → verify。锁定文件树后才能拿它安装测试，不能实测一个 DLL、交付另一个 DLL。生成 build-info 和发布清单；避免临时输出、游戏 DLL、存档、tokens、反编译原版源码混入发布包。

### F. 真实游戏与兼容矩阵

按 [RUNTIME-TESTING.md](RUNTIME-TESTING.md) 执行。最小矩阵：原版基线；loader+必要库；必要库+目标 Mod；目标 Mod+声明支持的可选 Mod。每种组合单独 lock；先测试最小依赖，再做成对高风险组合，必要时全组合。不要在全部 Mod 同时开启时开始定位首个错误。

区分单人和多人。多人相关功能需要实际主机+客户端、加入/重连、状态同步、保存/读档、权威端校验；没测就声明未验证。禁止因为 CUCoreLib 有多人适配就推断本 Mod 多人兼容。

### G. 交付

交付项：Mod-only payload、README（依赖/安装路径/升级卸载/存档影响）、版本与 SHA-256、兼容矩阵、已执行测试、已知限制、源代码位置。第三方依赖许可/NOTICE 随实际分发要求处理。发布只在用户要求发布时执行。

每个门禁独立状态：STATIC、BUILD、STAGE、BOOT、GAMEPLAY、SAVE_RELOAD、COMPATIBILITY。可取 PASS/FAIL/BLOCKED/NOT_RUN/NOT_APPLICABLE，并附证据或理由。编译成功不递推后续 PASS。

## 4. 推荐迁移顺序

1. 接入本工具的 profile 与哈希；旧项目内容代码不动。
2. 删除/显式关闭隐式构建复制；把部署变成独立步骤。
3. 固定已验证的 NuGet 版本，生成构建来源记录。
4. 准备隔离用户环境，手动或 UI agent 跑一轮正常玩法与存读档，形成基准证据。
5. 有重复测试痛点后，实现窄接口测试桥与案例 runner；不要先造大型平台。
6. 真实使用证明部署/回滚有需求后，按管理器事务规范补上 install/rollback。GUI 最后添加，复用同一 CLI 服务层。
