# 《最后一格电》完整第一关集中实施与交付

当前范围由 `Documents/ChapterOne-Implementation.md` 固定：应急干扰、正式UI与轻引导、核心声光和简洁结算。保留24×24四区地图、一个敌人及宽松数值，不增加新关卡或提高追击速度。

## 基线与工程边界

工程位于 `<PROJECT_ROOT>`，版本保持Unity 6000.3.28f1。完整基线在同级 `LastCellOfPower-chapter-baseline-20261112-231637`，共享文件每次修改前另行备份。

新内容集中于Assets/ChapterOne：独立场景、Gameplay/Level/Pulse配置、UI、声光、统计、字体音频与生成器。导航与布局复用不可变的已验收四区数据，旧场景不覆盖。官方TMP Essential Resources是现有uGUI包的必要资源，保留Assets/TextMesh Pro标准目录与其许可；不导入Examples/Extras。

ExpandedSession继续协调模拟与结局，通过少量C#事件连接独立UI、声音和统计。旧场景pulse/statistics为空、externalPlayerUI默认关闭，继续采用旧输入和画面行为。新版只有明确按钮恢复游戏；Q先于敌人捕获提交，成功才中断E并要求松键。

## 集中批次

| 批次 | 工作 | 当前证据范围 |
| --- | --- | --- |
| 核心 | Q边沿和费用、Disrupted恢复、Q/E事务、暂停门、统计快照 | pulse-checks.txt已PASS；使用虚拟设备和逻辑夹具 |
| 呈现 | 正式HUD/菜单、引导、区牌、整机高亮、状态灯、核心声音和设置 | presentation-checks.txt已PASS；preview-r2离屏检查中文与布局 |
| 整合 | 无Q与有Q完整行走路线、结果与重开清理 | 两条后台合成输入路线已PASS，无Warp或位置设置；Windows构建与后台启动已PASS；交付目录附最终哈希与核验结果 |

检查覆盖同帧救援、无效Q不扣费、按住不重复、遮挡、停止感知及保留记忆、Q取消E、UI模态不误恢复、声音生命周期、统计不重复和重开清理。只对具体新失败定向复核，不因一个组件改动就重复所有旧单房测试。

## 素材与复现

Noto字体固定在notofonts/noto-cjk提交 `f8d157532fbfaeda587e826d4cd5b21a49186f7c`，字体与OFL许可的URL和哈希在Fonts/SOURCE.json。十三个WAV由Tools/GenerateChapterAudio.py确定性生成，来源与哈希在Audio/SOURCE.json。TMP资源、LiberationSans OFL和EmojiOne署名随源码保留；没有从参考GitHub项目整包引入代码或音频。

生成前等待TMP Essentials实际导入完成，再建立字体图集和新场景。已有输出拒绝覆盖，修订使用独立输出目录；修复设备高亮前备份新场景，不重建原地图。实际命令和失败／成功日志保留在Evidence/ChapterOne。

## 交付与停止点

本轮Windows开发包为同级Builds/ChapterOne-20261113-011649，0错误0警告，后台启动退出0；源码与Windows独立归档，最终哈希及核验结果随交付目录提供。后台合成输入和离屏结果不冒充桌面人手操作或听感验收。

完成构建和汇总后只交一版给用户集中试玩，观察引导、音量、反馈与手感；不再要求用户逐项切窗口。旧单房遗留人工项保持历史未验，不继续追测。完成这一关后停止扩展。

## 本轮交付记录

ChapterOne-20261113-011649构建0错误0警告，后台包启动退出码0。核心、呈现、设备高亮、进度条、两条完整路线及重载清理已通过相应检查；本轮进入同一版集中试玩交付节点，声画主观体验与后台结果分开记录。
