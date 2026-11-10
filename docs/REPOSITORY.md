# 仓库与接手指南

本仓库保存 Wayfarer Mech 和 CU Mod Kit 工作流。首次纳管为 v0.4.0 开发基线，
不是稳定发布、完整 Starbound 行为复刻或多人兼容承诺。

## 新机器构建

需要 Python 3.13、.NET SDK 8、正版 Casualties: Unknown Mono 游戏文件和 CUCoreLib 1.1。
当前游戏指纹见 `docs/BASELINE.md`；本轮复核为 Steam build 26525321。
不从 GitHub 下载游戏程序集；从自己的游戏安装读取。

```powershell
python -m pip install -r requirements-dev.txt
python -m unittest discover -s tests -v
dotnet run --project tests/DrillShapeTests/DrillShapeTests.csproj -c Release

# 替换这两个本机路径；命令只构建，不安装或启动游戏。
$gameDir = 'D:\SteamLibrary\steamapps\common\Casualties Unknown'
$coreLibDll = 'D:\testGround\_casualties-analysis\moddeps\CUCoreLib.dll'
python tools/cumod.py doctor $gameDir
dotnet build src/WayfarerMech/WayfarerMech.csproj -c Release -p:RestoreLockedMode=true "-p:GameDir=$gameDir" "-p:CoreLibPath=$coreLibDll"
```

完整生成的 C# 目录、资源清单和原创 PNG 已纳管；正常构建不需要 Starbound 或 E 盘素材库。
`tools/build_starbound_catalog.py` 当前仍依赖作者本机的解包路径，只有重采集目录时才需要
配置该路径。`tools/art/gen_starbound_style_mech.py` 使用已纳管 JSON 生成原创图片。
测试启动、隔离和 UI 工具也仍有本机路径，换机器先核对，不能直接启动正式游戏做测试。

## 提交边界

- 纳管：源码、原创 PNG、生成器、目录 JSON、NuGet lock、测试、规范和文字测试报告。
- 本机保留：`.local/`、`runs/`、`artifacts/`、`versions/`、本机 profile、生成预览、
  原版素材、游戏 DLL、存档、日志、凭据。历史报告中的这些路径不会随 clone 出现。
- 不把 `git add -f` 用来绕过这些边界。需要发布玩家包时另行构建、stage、verify。
- 现阶段未选择开源许可证；私有代码管理不代表原版素材或第三方库获得再分发授权。

## 日常协作

`main` 保存可追溯基线，功能用 `feat/<name>`，修复用 `fix/<name>`。
提交说明写清行为变化和验证；合并前运行 Source checks。
CI 仅验证 Python/纯 C#，不会获得游戏文件、用户存档或本机凭据。
本机完整构建、游戏加载、真实交互、重启读档、兼容矩阵分别记录，不能由 CI 绿灯代替。
带有 gameplay 改动的提交须更新对应报告，并把未测项目保留为 NOT_RUN。

先看 `docs/WAYFARER-V0.4-REVIEW.md` 的待办，再读 mod-workflow skill。
首次只建立开发基线，不给候选版打稳定发布标签，也不上传 Workshop。
