# 信息来源与适用范围

采样时间：2026-11-10（Asia/Hong_Kong）。下面是本机已观察到的基线，不是对以后版本的保证。

## 本机读取

- 游戏根：`D:/SteamLibrary/steamapps/common/Casualties Unknown`。
- `appmanifest_4576490.acf` 的 buildid：`26525321`。
- `CasualtiesUnknown_Data/Managed/Assembly-CSharp.dll` SHA-256：`de2462a80d1a6d0a95a3d8032589a1b30e784853cb6bbd3951a52eeb67fc7335`。
- 安装中有 `MonoBleedingEdge`，未看到 BepInEx 安装。工程基线为 Unity 2022.3.62f3、net48、BepInEx 5.4.23.6、CUCoreLib 1.1；运行时是否匹配仍需启动日志确认。
- 外骨骼工程：`D:/testGround/casualties-mecha-mod`；其 HANDOFF 明确尚未实机验证。此次未重编译或修改该工程。
- 对已读取的工程 Target 和 PackageReference 的问题分析见 WORKFLOW；package 脚本是源码证据，不表示本次执行过它。
- 本地 API/逆向资料：`D:/testGround/_casualties-analysis/moddeps/docs-*.json`、`CUCoreLib.xml`、`decompiled/Assembly-CSharp.cs`。资料可能来自不同时间的库文档，使用前必须对当前 DLL 成员签名核对。
- 旧 RimWorld 文件只用于识别最初游戏指代，不作为本游戏 API 依据。

## 一手参考（本次已查询）

- [CUCoreLib 项目](https://github.com/jimmyking9999999/CUCoreLib)：本游戏的 BepInEx 扩展库与官方文档入口；版本按本机锁定，不直接追 main。
- [BepInEx 插件工程指南](https://docs.bepinex.dev/articles/dev_guide/plugin_tutorial/2_plugin_start.html)：插件身份、游戏程序集引用和加载验证。
- [Unity 2022.3 persistentDataPath](https://docs.unity3d.com/2022.3/Documentation/ScriptReference/Application-persistentDataPath.html)：用户数据路径为何不随游戏目录副本隔离。

管理器事务、profile schema、测试桥协议与验收状态是本工具包提出的设计，不是上述项目已提供的功能。不得据文档草案对外声称桥/自动回滚已可用。
