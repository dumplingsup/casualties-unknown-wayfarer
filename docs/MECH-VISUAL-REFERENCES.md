# 下一轮机甲造型参考

2026-11-10。用户不认可 v0.2.1 外观；该版只是工程结构改善，不作为最终美术定稿。本次仅检索参考，不更改运行时资产。

## 最新用户方向：可见主角的开放驾驶舱

用户进一步指定参考 Starbound 鸟人族，希望看见主角本体与面部表情。此方向优先于下面旧的封闭装甲舱建议。

- 对应种族机体是 **Champion** 系列，不是 Avali 模组种族。
- [Champion-00](https://starbounder.org/Champion-00_Mech_Body) 的页面明确描述为 Avian 驾驶员设计的 open-top 原型。
- [Champion](https://starbounder.org/Champion_Mech_Body) 是同系列升级版，页面描述为 Avian 战士形象；不要把升级版和原型都笼统宣称同一种开放结构。
- 原型图：https://starbounder.org/mediawiki/images/5/56/Champion-00_Mech_Body.png
- 升级版图：https://starbounder.org/mediawiki/images/7/76/Champion_Mech_Body.png

建议设计：开放座舱露出角色完整头部和部分上身，低前护板遮住腿和坐姿接缝；后侧座椅/防滚架支撑角色，工具挂在两侧且低于脸部，不在头顶架横炮。保留本游戏角色比例与外观，机体用工业材质；借鉴开放驾驶关系，不照搬鸟人图腾或金色装饰。头部需要保持原生像素尺寸与足够显示面积，不把它缩成装饰小点，也不隔着深色玻璃。

### 本地实现依据（调研，尚未实现）

原版 `FacialExpression` 有独立 head/eyes SpriteRenderer、眨眼计时，以及依据 Body 状态选择眼睛/头部精灵的逻辑。当前 MechController.Enter/FollowPilot 会隐藏所有驾驶员 renderer，Plugin 又阻止驾驶员 Body.Update/FixedUpdate 和 Limb.Update；直接启用全身 renderer 不能获得稳定坐姿，直接恢复 Body 全流程会改变现有保护与物理语义。

优先验证一个无物理、无物品/行为脚本的驾驶员显示层：映射实际角色的头部、眼睛、外观配件与必要上身 renderer，在原版表情更新之后同步 Sprite、颜色和所需材质属性，安放在座舱锚点。严禁 Instantiate 完整 Body 造成重复逻辑，也不要画固定假脸替代用户角色。第一步需核查各角色类型的 renderer 层级与表情更新时间，不能只靠对象名字猜头部。

分开测试“原版眨眼可见”和“表情随状态变化”。Body 更新被冻结时部分表情计时器也被冻结；需要窄范围处理视觉计时/状态来源，不能因为 eyes.sprite 每帧刷新就宣称全部状态表情正常。原生伤痕/遮挡装备应按角色实际外观保留，不为了露出脸擅自卸除头盔。可见性不自动意味着驾驶员重新受伤，本轮不损坏的玩法约定保持独立。

遮挡层序：后侧机体/座椅 → 主角头部与上身 → 前护板/近侧操纵杆；脸不被武器、火焰或 HUD 遮住。左右朝向、耳朵/头发溢出、上下机恢复、切层、重启读档必须逐项验收。

下一步先做带真实角色比例的开放座舱概念稿，确认造型后再改运行时显示系统。当前 v0.2.1 仍为封闭舱候选，不代表已满足这项新要求。

## 参考来源

1. **Metal Slug / Slug Gunner：主造型参考。**
   - SNK 官方图库：https://game.snk-corp.co.jp/official/ms_gallery/
   - 官方插画：https://game.snk-corp.co.jp/official/ms_gallery/img/pa/pa_040.jpg
   - 侧视像素动画参考：https://www.spriters-resource.com/game_boy_advance/msadv/asset/11048/
   - 借鉴：饱满装甲舱体、明确的关节圆盘、非人形机械脚、前后错位的腿、工具与主体的连接。不要照搬炮塔、徽记或素材像素。
2. **Starbound：驾驶舱与模块化工具的分工参考。**
   - 官方组装介绍：https://playstarbound.com/26th-april-some-assembly-required/
   - 机甲总览：https://www.starbounder.org/Mechs
   - 图例：https://www.gamersdecide.com/articles/starbound-best-mechs
   - 借鉴：能看懂乘员位置、左右模块不同职责、武器与身体之间留有间隙。不要混淆原版、模组和同人图，也不沿用高饱和金色配色。
3. **Into the Breach：小尺寸轮廓与功能识别参考。**
   - 官方页面：https://subsetgames.com/itb.html
   - 官方截图：https://subsetgames.com/img/itb/ITB_ss_5.png
   - 仅参考明暗分组和机型差异；等距视角不能直接用于本项目侧视精灵。

这些是视觉参考，不是已获授权可打进 Mod 的资产包。原图保持在线来源，不混入原创 Assets。

## 对当前图的审美判断

- 驾驶舱高而直，主体近似矩形柜体；窗也没有足够明确的朝向。
- 腿像裤腿，脚像鞋，缺少工程机械的关节与支撑特点。
- 躯干、钻臂、腿几乎都横平竖直，静止时没有重心和姿态。
- 细碎高光与轮廓线多，但大块体积与主次不清；像多个独立小图拼起来。
- 修正 pivot 和增加螺丝不能代替设计轮廓。

## 推荐的新方向（建议，尚未经用户选定）

保留采矿载具定位：饱满、略前倾的装甲驾驶舱，短腰连接外露髋轴；机械反曲腿和分段支撑脚；一侧粗短钻臂、另一侧紧凑机炮；喷口融入后部设备舱。低饱和灰绿为主体，少量旧工程黄，窗和工作灯仅作局部焦点。

先做三张**真正不同轮廓**的侧视概念稿：圆舱重型、楔形工程型、轻型反曲腿。先看黑色剪影，再看灰度体积，最后配色和像素细节。用玩家大小和实际洞穴背景对比，不先拘泥于旧部件画布。选出造型后再重新规划碰撞体、关节、工具轴线；不能把已验证的碰撞尺寸当作一切美术设计的先验限制，也不能未经测试随意更改。

本轮不再用矩形/椭圆生成器直接试错最终外观。先建立可信的概念稿，再制作原生像素分层资产；程序化工具负责装配、检查与导出。
