# 《最后一格电》战术信息与操作反馈完善版

本版已完成独立场景、设备配对标识、真实响应记录、门取消解释、短提示和三张帮助卡。保留双机器人、全部速度与电量、装置次数、地图碰撞、导航及供电逃生目标。本轮只交付这一版集中试玩，玩家是否更容易理解工具仍需实际试玩观察。

## 启动与文件

交付目录为 `LastCellOfPower-TacticsPolish-20261114-224319`。直接运行 `Windows/LastCellOfPower.exe`；分享或移动时使用完整的 `ChapterTacticsPolish-Windows.zip` 并全部解压，不要只复制 exe。该包为 Windows x64 Development、Mono、Unity 6000.3.28f1，入口为 `WarehouseTacticsPolish`。

`ChapterTacticsPolish-Source.zip` 包含 Assets、Packages、ProjectSettings、源码工具、配置、原始素材来源、文档和本轮精选证据，不包含 Library 缓存。`SHA256SUMS.txt` 是两个压缩包的 SHA-256；`delivery-verification.json` 给出每个文件的哈希、大小和压缩包 CRC 检查结果。

运行时按 WASD 移动、鼠标观察、E 交互、Q 单体干扰、Esc 暂停、R 在结局重开。开场和暂停菜单可以打开操作说明。目标仍是补电、给终端供电、从北侧出口离开；可自行选择绕行或使用工具，无须用完装置。

## 本版变化

| 内容 | 玩家能看到的结果 |
| --- | --- |
| N01 / N02 配对 | 控制台和远端扬声器同号；牌面写明设备区南侧或货架区南侧扬声器 |
| 世界牌与提示 | 小牌面提供编号、目的地、短状态和工作灯；接近设备时 HUD 显示倒计时、次数和响应记录 |
| 真实响应 | 只有机器人实际接受本次调查后才累计，且每台只计一次；文字使用“曾响应”，不保证脱离危险 |
| 无响应 | 信号运行时为“暂未收到响应”，结束后仍为零才显示“本次未收到响应” |
| 临时门 | 区分门口占用拒绝、关闭途中取消、闭锁、回开与用尽；拒绝和取消明确显示次数保留 |
| 短提示 | 成功默认 2 秒，拒绝和取消 3 秒；同事件去重、失败优先，暂停和失焦保留剩余时间 |
| 轻引导与帮助 | 首次安全接近显示最多 6 秒教学；警觉或追击中收起长教学；帮助页有 Q、诱导与门三张卡 |
| 重开与兼容 | 清空本局响应和旧反馈；已看教学保留到本次程序退出；旧场景未配置本主题时继续原表现 |

## 配置与复现

新内容集中于 `Assets/ChapterTacticsPolish`。场景为 `Scenes/WarehouseTacticsPolish.unity`；`TacticalReadabilityProfile` 保存设备映射、文案、颜色及 2/3/6 秒呈现时长。`PolishFeedbackPresenter` 只读取事实，`DeviceReadabilityView` 只更新文字和材质属性块。设备状态、次数和机器人判断继续归原系统管理。

从源码项目根目录使用现有 Unity CLI。环境和依赖按 ProjectVersion、manifest 与 packages-lock 固定，不需要安装参考仓库。

```powershell
unity run . --command 'polish inspect' --no-tail --timeout 420 --format json
unity run . --command 'polish verify' --no-tail --timeout 420 --format json
unity run . --command 'polish presentation' --no-tail --timeout 420 --format json
unity run . --command 'polish route' --no-tail --timeout 420 --format json
unity run . --command 'polish build' --no-tail --timeout 900 --format json
```

`polish generate` 从指定墙面修复基线生成；目标已存在时执行检查，不覆盖现有交付场景。修补入口先备份候选再检查。构建输出到新的时间戳目录。原墙修复场景、修复网格、导航和之前交付包均保留。

## 实测与边界

| 层次 | 本次结果 |
| --- | --- |
| 静态保护 | 原变换、碰撞、导航、AI/资源/装置配置、修复网格、照明和相机一致；新增六块标识无 Collider、NavMeshObstacle 或 Light |
| 后台逻辑 | 10 组通过：0/1/2 真实响应、去重、优先级、过期、提示时长、门结果、冻结与重开、禁用及两个旧场景兼容 |
| 呈现 | 2 组通过：静音操作反馈、门移动占用取消、真实灯色、三分辨率 HUD/帮助；近距 0.85/1.2/1.8 米字形视口检查 |
| 连续输入 | 1 条合成键鼠路线通过，实际控制器行走约 34.97 米，充电撤退、暂停恢复、诱导、Q、供电、逃生均完成；未修改角色坐标 |
| Windows 构建 | `ChapterTacticsPolish-20261114-223021`，0 错误、0 警告 |
| 包内实际渲染 | RTX 4060 Laptop GPU，Direct3D11，1920×1080，100% 渲染比例；预热 60、采样 600 帧，平均 5.240 ms、P95 6.087 ms，退出码 0 |
| 普通启动 | 从交付 Windows 副本启动，记录到 `EXPANDED_READY`；本次窗口工具启动失败，普通点击流程未验证 |

性能数据测量固定机位 `Camera.Render` 加同步回读，玩法运动被冻结，不是正常游玩 FPS，也没有独立 GPU 计时。离屏呈现截图临时将 Canvas 从 Overlay 转为 Camera 模式；三种尺寸的可见排版证据不等于桌面所有显示设置均通过。

编辑器 PlayMode 检查退出阶段记录了 Input System 的 `m_Settings != null` 断言。各组行为断言已完成；这是保留的检查清理问题，不能宣称编辑器日志零异常。开发包图形探针未记录运行时 Error/Exception/Assert，正常退出。普通启动冒烟进程因窗口控制不可用而在取证后清理，未宣称普通菜单退出通过。

完整说明见 `Acceptance.md`，源码内证据位于 `Evidence/ChapterTacticsPolish`。旧的失败尝试和大体积快照仍在原工程保留，精选源码包不收录这些大文件；对应哈希及保护摘要随包提供。

## 集中试玩

按自己的方式完成一次逃生即可，不预先演示路线，也不要求使用全部工具。观察是否能分清诱导、封门和 Q；是否理解声音来自远端；发生门取消时是否知道原因和次数仍在。未接触某工具或未发生取消，就把该项记为未验证。理解规则但觉得工具无用时，下一轮再讨论摆位或平衡，本版不顺带改数值。

## 来源和开发流程

每次开发前检查 [game-development-plugins](https://github.com/yq6666-66/game-development-plugins)。本轮恢复及交付时 main 均核对为 `8693e66d50f40b70f95fec3f77f3a0322ef08eb3`，采用玩法闭环、视觉迭代、工程状态所有权、UI 与构建交付相关流程。首次九项方案流程只在首次开工或接手时执行；本项目后续迭代按已授权范围和该仓库适用流程推进。

本轮没有新下载的第三方美术或运行时依赖。Windows 的 `ThirdPartyNotices` 继续携带现有 CC0 素材来源、原创机器人来源、Noto/Liberation 字体许可和 EmojiOne 署名；源码保留原始来源与哈希。没有上传或对外发布。
