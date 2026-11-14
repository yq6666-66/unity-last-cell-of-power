# LastCellOfPower / 最后一格电

当前主线为 Windows 第一人称 **ChapterOneArtRealism 写实废弃仓库与锈损巡检机版**：沿用完整第一关的24×24四区、宽松潜行、应急干扰和结算，完成真实货物模型、受潮墙面、封闭屋顶梁架、局部照明、房间反射和原创破损机器人。技术基线保持 Unity 6000.3.28f1、URP、Input System、AI Navigation、uGUI＋TMP。最新说明见 `ChapterOneArt-Realism-Delivery.md`，证据见 `../Evidence/ChapterOneArtRealism/Acceptance.md`。

## 打开与操作

工程位于 `<PROJECT_ROOT>`。用 Unity Hub 或 `unity open` 打开工程，当前主场景为 `Assets/ChapterOneArt/Scenes/WarehouseChapterOneArtRealism.unity`。本轮 Windows 开发包为工程同级 `Builds/ChapterOneArt-20261113-164659`，构建0错误0警告。最新说明见 `ChapterOneArt-Realism-Delivery.md`，证据见 `../Evidence/ChapterOneArtRealism/Acceptance.md`。原 ChapterOne、WarehouseExpanded、EscapeSample 和 StealthSample 场景及开发包继续保留。

| 输入或界面 | 行为 |
| --- | --- |
| 开始／继续按钮 | 明确开始或恢复，捕获鼠标；点击滑条或其他界面不会恢复玩法 |
| WASD／方向键 | 相对视角移动，也可立即取消充电或供电并撤退 |
| 鼠标 | 转向与俯仰，交互期间仍可观察 |
| 按住 E | 对准可达的充电座或供电终端交互；松开或移动可中断 |
| Q | 对4米内无遮挡且处于警觉或追击的敌人释放一次应急干扰 |
| Esc | 暂停并释放鼠标，取消交互；设置与帮助界面返回暂停菜单 |
| F1 | 切换开发诊断，不替代正式 HUD |
| R／再来一局 | 在被抓、耗尽或逃脱后重开 |

失焦会暂停。返回后用继续按钮恢复，先松开移动键、E 和 Q；按住的旧输入不会自动续充或发射。暂停菜单可重看操作说明，调整主音量和鼠标灵敏度；重开不重复强制显示开场说明。

## 目标与选择

先在维修起步区补充一些电量，再到西北设备区终端供电，最终从东北装卸区北门离开。四区连接口有名称、编号与色带，设备在可交互时高亮。初始40电量只接近供电门槛，仍须预留撤离和应急干扰余量。

电量上限100；静止消耗0.2/秒，实际移动额外0.3/秒，充电净增7.8/秒。终端需至少36电量，按住E连续2秒后扣35，门用1秒打开，实际走过出口区域才算胜利。

Q需至少13电量，成功命中扣12，敌人停机2.5秒，冷却15秒。无需准星瞄准，但必须是Alert或Chase中的有效威胁；无目标、被遮挡、距离过远、冷却中或电量不足均不扣费、不启动正式冷却。成功Q才取消E并要求松键；失败Q不会打断充电。停机不清空已确认追击记忆，恢复后按真实可见性继续决策。

结算显示本局有效用时、剩余电量和成功干扰次数。用时不计开场、暂停与结算，无效Q不计次数。灯光与声音只提供反馈，不引入暗处隐身或声音吸引敌人的额外规则。

## 配置、素材与验证

`Assets/ChapterOne/Config` 保存独立的关卡、宽松玩法和干扰配置；运行时状态不写回资产。规则以 `GameDesign.md` 为准，实施边界见 `ChapterOne-Implementation.md`，证据状态见 `Progress.md`。

Noto Sans CJK SC Regular 与 SIL OFL 1.1许可随工程提供，固定来源及哈希在 `Assets/ChapterOne/Fonts/SOURCE.json`。十三个机械／电子音效为原创确定性合成，生成脚本为 `Tools/GenerateChapterAudio.py`，音频哈希在 `Assets/ChapterOne/Audio/SOURCE.json`。TMP Essential Resources来自已安装的官方uGUI包，保留其LiberationSans OFL和EmojiOne署名文件，未导入Examples/Extras。

核心脉冲与呈现检查已通过；不使用Q和使用一次Q的后台行走路线均进入Results，未设置角色位置或Warp。1280、960、1920宽度的离屏预览已检查中文与布局。上述是后台合成输入与离屏证据，不冒充桌面人手操作或主观听感验收。本轮构建和最终包哈希待交付收尾登记。

## 保留范围

旧四区、单房场景与包保留，旧单房遗留的人工项目仍如实留档，不追补为PASS。开发按后台合并检查和最终一版集中试玩推进，不再要求逐项切窗口。当前没有第二敌人、配音、历史排行榜、挑战解锁、随机地图、手机或网络功能，不对外发布。
