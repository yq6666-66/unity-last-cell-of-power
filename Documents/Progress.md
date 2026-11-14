# 进度

当前主线为 ChapterOneArtRealism 写实废弃仓库与锈损巡检机版。真实CC0货物模型、受潮墙面、已烘焙房间反射、可读的近处照明和原创破损机器人已完成；两条后台路线与重开检查通过。Windows包 ChapterOneArt-20261113-164659 构建0错误0警告。RTX4060/D3D11/1080p显式渲染+同步读回600帧平均4.977ms、P95 5.589ms；不是正常长时间游玩帧率，独立GPU计时不可用。最新说明与证据见 `ChapterOneArt-Realism-Delivery.md`、`../Evidence/ChapterOneArtRealism/Acceptance.md`。以下保留原ChapterOne基线记录。

## 当前第一关

| 项目 | 状态与证据边界 |
| --- | --- |
| 场景与配置 | Assets/ChapterOne/Scenes/WarehouseChapterOne.unity已生成，使用独立Gameplay/Level/Pulse配置，保留四区布局与导航 |
| 编辑模式契约 | PASS：Evidence/ChapterOne/contract.txt，引用、字体许可、默认值、UI归属与音频集合 |
| 核心脉冲与统计 | PASS：pulse-checks.txt、verify.log；Q门控、门槛、遮挡、Q/E事务、同帧救援、停机记忆与统计 |
| 呈现与模态 | PASS：presentation-checks.txt、presentation-r2.log；填充条、模态点击/设置、暂停音频策略、结果统计与重开清理 |
| 设备高亮 | PASS（绑定与恢复检查）：4个设备整机各8个Renderer，repair-highlights.log及呈现检查；不再只绑定锚点marker |
| 字体与素材来源 | Noto字体/OFL固定提交与哈希记录，十三个原创合成音效及生成脚本齐；审计哈希匹配 |
| 离屏布局 | 已检查intro-1280、settings-960、hud-1920、results-1280；preview-r2.log。不是人手试玩截图或听感验收 |
| 无Q完整路线 | PASS：route-no-pulse.txt，33.316米、余电15.861、0次干扰、Results，无Warp/位置设置 |
| 有Q完整路线 | PASS：route-with-pulse.txt，66.224米、1次成功Q、余电13.818、Results，无Warp/位置设置 |
| 重开清理 | 合并runner断言通过：恢复电量40、暂停、冷却/停机/捕获/统计清零、唯一UI且无游戏音频残留 |
| Windows构建与启动 | PASS：同级Builds/ChapterOne-20261113-011649，0错误0警告；startup-smoke退出码0，UI=Intro，Failure空 |
| 最终交付包与哈希 | 源码与Windows包独立归档，CRC/SHA256核验结果随交付目录提供 |

本轮证据位于Evidence/ChapterOne。核心与呈现使用受控坐标、虚拟设备或UI方法；两条路线使用合成键鼠经过正常控制器行走，导航只给方向，不改玩家或敌人位置。后台执行时间不等于人类通关时长。最终手感与音量由用户在同一个Windows包集中体验，不逐项打断用户。

## 保留基线

WarehouseExpanded四区宽松版已完成自身范围的后台逻辑、完整行走路线、三结局重开、Windows构建和启动检查，并获用户验收确认。旧包为同级Builds/WarehouseExpanded-20261112-220355，相关证据在Evidence/WarehouseExpanded。短/长静态路线约15.97/39.50米，后台通关行走33.411米、余电15.808；这些结果不能代替新ChapterOne检查。

EscapeSample单房资源与逃生样本有自动资源/交互/场景及部分授权Windows输入记录；充电、供电开门胜利、被抓/耗尽及重开、供电中断曾通过。充电被抓、切窗取消恢复和该单元搜索返回等人工项目未全部覆盖，保留在Evidence/EscapeSample历史记录，不补造PASS，也不继续追着旧单房验收。

StealthSample单房潜行完成其自身范围的控制/AI与授权Windows输入事件验收。Stage4/Unit1仍有历史未覆盖项；旧idle方向键来源未定因，KEY_MISSING不构成外部复核。原场景、导航和配置保留；本轮只读审计对基线13个旧scene/nav/config哈希无差异。

## 当前收尾

登记ChapterOne独立Windows构建与启动结果，更新汇总和哈希后交付一版集中试玩。保留后台与用户体验结论的边界，不新增敌人、网络、手机、挑战解锁或历史排行榜。

## 交付边界

实现及后台集中验证完成；画面已做960/1280/1920离屏核对，音频已验证资源、事件与暂停策略。离屏预览、合成路线和无窗口包启动不冒充桌面人手操作或主观音量确认。最终用户集中试玩同一Windows包后再决定下一轮调整，不再扩展关卡或追加技能。
