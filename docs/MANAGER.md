# 简易 Mod 管理器规范

## 已实现的 v0

`tools/cumod.py` 只依赖 Python 标准库。提供 doctor、lock、stage、verify；管理 profile 中显式列出的 Mod 组合。无安装、游戏控制、网络下载和隐式清理。

它能做：启停组合（编辑 enabled）、精确版本依赖检查、声明冲突检查、依赖/after 拓扑排序、Windows 大小写文件冲突检测、路径穿越拒绝、输入与游戏哈希锁定、ZIP 指定成员读取、独立目录打包、完整性验证。排序只是依赖计划，不改变 BepInEx 加载行为；插件还必须正确声明 BepInDependency，Harmony patch 顺序单独处理。

profile 中未声明的语义依赖/冲突不会被发现。DLL 的 GUID 与声明的版本尚未自动反射核对，发布前要通过程序集元数据或游戏加载日志核对。没有自动把缺失依赖补进 enabled，避免偷偷改变测试组合。

## profile schema 1

```json
{
  "schema": 1,
  "game": "casualties-unknown",
  "name": "content-minimal",
  "build": "26525321",
  "fingerprints": [
    {"source": "C:/Games/CU/CasualtiesUnknown_Data/Managed/Assembly-CSharp.dll", "sha256": "填入已核对的64位小写SHA256"}
  ],
  "enabled": ["bepinex.loader", "net.cucorelib", "author.content"],
  "components": [
    {"id": "bepinex.loader", "version": "5.4.23.6", "files": [
      {"source": "../deps/BepInEx.zip", "member": "winhttp.dll", "target": "winhttp.dll"}
    ]},
    {"id": "net.cucorelib", "version": "1.1", "requires": {"bepinex.loader": "5.4.23.6"}, "files": [
      {"source": "../deps/CUCoreLib.dll", "target": "BepInEx/plugins/CUCoreLib.dll"}
    ]},
    {"id": "author.content", "version": "0.1.0", "requires": {"net.cucorelib": "1.1"}, "after": [], "conflicts": [], "files": [
      {"source": "../build/Content.dll", "target": "BepInEx/plugins/author.content/Content.dll"}
    ]}
  ]
}
```

上面是字段示意，loader 只列了一个成员，不能用于完整安装；实际 loader 的文件集合见本机生成示例。`bepinex.loader` 是管理器组件 ID，不是 BepInEx 插件 GUID。

- `source` 相对 profile 所在目录解析，也可绝对路径；lock 保存绝对 source，供本机 staging 使用。移机后重新定位输入生成新锁，已生成的 payload 可独立校验。
- `member` 可选，表示 ZIP 内精确文件名；不会执行 ZIP 内脚本，也不会按归档路径直接解压。
- `target` 是游戏根目录下的相对路径；拒绝绝对路径、`..`、反斜线、Windows 保留名等。每个路径只能有一个 owner，即使内容相同也拒绝覆盖。
- `sha256` 在 source 文件声明中可选，有则作为预期输入校验；无则 lock 时计算。fingerprint 的 SHA-256 必填。hash 是完整性检查，不是签名或恶意内容检测。
- `requires` 用精确版本，简化解析并确保实验可复现。未来可增加范围解析器，但最终 lock 仍必须精确；不按文件时间戳选择版本。
- `after` 是“若启用则排其后”，不会引入依赖；`conflicts` 是不可共存组件 ID。
- `build` 是声明信息；硬门禁为 fingerprint。doctor 提供当前 build 供核对。

stage 限制单文件 256 MiB、合计 512 MiB；适合小型 Mod 集。超过时明确报错，应分包或扩展流式实现。输出只能是新目录；COMPLETE 最后写入。崩溃留下的目录不算成功、不覆盖重用。verify 会检查缺文件、多文件和篡改。

不同配置独立生成 stage，不向 plugins 下保留 `.disabled` 子文件夹；加载器可能递归扫描，真正禁用的 DLL 应在部署树外。

## 后续部署接口：规范，尚未实现

建议新增命令只围绕同一份 lock，不另造 UI 专有状态：

```text
plan-install <verified-stage> <game-root>  # 输出新增/替换/删除/冲突
apply <plan-id>                           # 显式应用已核对计划
status <game-root>                        # 对比部署账本与真实文件
rollback <transaction-id>                 # 恢复文件及原先“不存在”的状态
```

部署不是简单 Copy-Item -Recurse。事务最小要求：

1. 确认目标根、game fingerprint、游戏已退出；对目标持有排他锁。根和每个祖先经过 resolve 校验，拒绝目标子树 junction/symlink；目标只允许游戏根内的清单路径。
2. 计划列明每条路径 old/new 哈希、owner、原先存在与否、备份位置；不属于本管理器的冲突文件默认不覆盖，交给用户选择迁移或更换 profile。
3. 停用组件仅删除账本里属于该组件且仍匹配上次部署哈希的文件；用户后来修改的文件记冲突。不整目录删 BepInEx/plugins 或 BepInEx/config。
4. 所有替换先备份、再写同卷临时文件、再 rename；多文件不可能靠一次 rename 自动保证事务，要 journal 记录 PREPARED/APPLYING/COMMITTED/ROLLING_BACK/ROLLED_BACK/CONFLICT。
5. 每步持久化 journal，进程崩溃后恢复未完成事务；不得把“已经部分复制”报告成成功。失败自动尝试回滚，回滚失败留下可诊断账本和备份，不清理证据。
6. rollback 前比较“当前值 == 本事务写入值”；若被游戏、用户或其他管理器改变，拒绝覆盖并报告冲突。只恢复此事务触及的文件。
7. 配置和存档独立管理。回滚 DLL 不等于回滚存档 schema，不能自动用旧存档覆盖玩家的新进度。

部署要有有意义的故障测试：复制第 N 文件失败、空间不足、游戏占用、并发 apply、用户中途修改、重解析点、配置漂移、缺备份、退出后恢复、插件从组合中移除。未实现这些之前，v0 保持离线输出是明确的产品边界。

## 高通用性与扩展点

默认组件是普通文件映射：BepInEx 插件、CUCoreLib 扩展、语言文件、音乐都能表示。loader/patcher 也可映射但风险更高，计划应显示类型；不得假定所有 Mods 都是一个 DLL。

后续增加可选 adapter：`inspect_metadata`（GUID/依赖/程序集），`validate_content`（游戏特有注册表/语言/资源），`launch_test`（验证过的数据路径隔离），`collect_evidence`。核心只认识规范化输入输出，不内置 Titan 物品名称。

兼容信息单独维护：tested game hashes、dependencies、conflicts、patched methods、registered IDs、save schema、optional integrations。文件无冲突只能叫“文件层可组合”，真正兼容以游戏测试矩阵为准。

不强制所有社区 Mod 改用此 manifest：可建立外部包装 manifest，保留原包内容和来源哈希。自动下载、市场、GUI、远程控制都不是首版必要条件。
