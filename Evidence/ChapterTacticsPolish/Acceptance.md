# 战术信息与操作反馈完善版：证据汇总

日期：2026-11-14。有效基线：`Assets/WallSurfaceFix/Scenes/WarehouseTacticsWallFix.unity`。候选：`Assets/ChapterTacticsPolish/Scenes/WarehouseTacticsPolish.unity`。Windows 构建：`ChapterTacticsPolish-20261114-223021`。方法参考固定提交 `8693e66d50f40b70f95fec3f77f3a0322ef08eb3`。

本报告区分静态合同、受控夹具、离屏画面、正常控制器的合成输入、Windows 实际图形和玩家理解。工程交付准备已完成；压缩包自检以交付外层 `delivery-verification.json` 为准。玩家理解假设尚未验证，不计为后台通过。

## 逐项审核

| ID | 合同 | 实际证据与结论 |
| --- | --- | --- |
| R01 | 保留原玩法和物理布局 | `contract.txt`、`protection-summary.json`、`baseline-file-audit.json`；原文件只有四个授权共用脚本改变，无缺失文件。编辑器自动增写 URP 路径字段已备份恢复 |
| R02 | N01/N02 配对与目的地 | 场景绑定检查；`Presentation/muted-noise-activated-1280.png`、`world-sign-1p2m-1280.png`、`world-sign-1p8m-1280.png`；完整目的地可见 |
| R03 | 状态/倒计时/灯/文字 | `presentation-checks.txt`；世界牌短状态，实际同目标 HUD 承载详细信息；已核真实 MPB 灯色 |
| R04 | 实际接受后去重0/1/2 | `verify-checks.txt` 对应三组；`TryInvestigateNoise` 成功之后才记录，事件/帧号关联 |
| R05 | 准确措辞/无位置泄露 | 运行中零响应、结束零响应、1→2 真实响应和结束历史由实际 HUD 断言；新设备反馈日志不含角色位置，不推断失败原因 |
| R06 | 门拒绝/取消与消耗 | 逻辑覆盖拒绝、取消、忙碌、闭合、回开和用尽；呈现使用合成 E 和 CharacterController 移动进入门口触发取消 |
| R07 | 单槽2/3秒/优先级/去重 | 实际 HUD 成功在1.99秒可见、2.01秒清空；拒绝在2.99秒可见、3.01秒清空。失败优先、事件去重、结局清理有断言 |
| R08 | 暂停/失焦/R | 逻辑测试冻结并续显、清旧事件和响应、R恢复状态；应用内已看教学不重复 |
| R09 | 安全短教学/威胁压制 | 逻辑检查安全、危险与重开条件；离开3米和切换设备退出分支有源码复核，未单列专项运行断言 |
| R10 | 三卡/双威胁/尺寸 | 960×540、1280×720、1920×1080 的帮助与双威胁截图、实际 TMP 缺字溢出检查；1280转头前后背景对照 |
| R11 | 可选表现与旧版 | 禁用/重新启用呈现与 MPB 恢复检查；`WarehouseChapterOneArtRealism` 和 `WarehouseTactics` 窄兼容通过，不替代全历史版本重新验收 |
| R12 | 标牌物理和叠面 | 六牌无 Collider/NavObstacle/Light；文字与底板前面分离34.5mm；已检查三角面无共面重叠。旧修墙网格引用与启用状态一致 |
| R13 | 连续操作 | `route-final.log`、`route-normal-input.txt`；虚拟键鼠、模拟焦点、显式Session.Tick、真实控制器和NavMeshAgent。约34.967m，余电26.233，一次诱导、一次Q，逃脱；无Warp/位置设置 |
| R14 | 独立包实际运行 | `build-result.txt`、`gpu/tactics-render-probe.json`、`normal-startup-result.json`；构建与图形PASS、普通就绪PASS，桌面交互未验证 |
| R15 | 源码/许可/绑定/哈希 | `Tools/PackageTacticsPolish.py` 按文件哈希和ZIP CRC验证；最终数值见外层SHA256SUMS及delivery-verification，不用文件名替代身份 |

## 运行命令与结果

工作目录是项目根目录。使用现有 Unity CLI beta17 和 Unity 6000.3.28f1。以下 CLI 均结束且无遗留 Unity Editor：`polish verify` 10 组通过（verify-final.log）、`polish presentation` 2 组通过（presentation-compact-signs.log）、`polish route` 1 组通过（route-final.log）、`polish build` 成功（build-final.log）。源生成、牌面修复及对应失败保留历史，不计作最终成功。

Windows build-result 记录 0 错误、0 警告。显式 `-tactics-render-probe` 从新包启动，使用 D3D11 和实际 RTX 4060 Laptop GPU，1920×1080、renderScale=1、后处理开启，9个固定机位含两台机器人，预热60帧、采样600帧。平均渲染加同步5.240422ms，P956.0872ms，GPU独立计时0样本，进程退出0。全部视图像素非空；主审另查看出生与东口图。

普通启动使用交付副本Windows目录作为工作目录，无probe、无batchmode。日志出现 `EXPANDED_READY energy=40 spawn=(-8.00, 0.00, -9.00) patrolStops=8`。两种桌面工具入口均在初始化时报 `failed to write kernel assets: 系统找不到指定的路径。 (os error 3)`，因此没有声称本轮桌面按键/按钮/普通退出已通过。工具失败没有修改游戏或系统；冒烟进程取证后清理。图形探针退出0与该普通进程清理分开记录。

## 画面复核与限制

第一次大牌近距图可见遮挡和尺寸问题；之后保留原0.85m机位，改为0.62×0.34m紧凑牌，补1.2/1.8m同向图。世界牌承担编号、完整目的地和短状态；详细倒计时、响应和次数迁到真实HUD断言，未删除信息覆盖。主审查看近距、1.8m、门取消和960帮助图，未见文字裁切；取消图同时显示次数保留与正在回开。帮助主卡改为不透明后未见世界文字透出。

离屏捕获临时转换Canvas模式，不能当成桌面Overlay逐像素证明。静音是AudioListener静音，设备行为来自真实提交；合成路线不是实体键盘验收，不把18.42秒自动操作时长当作玩家首通时间或5～8分钟体验结论。

检查结束阶段 Input System 编辑器钩子在 `InputManager.ApplySettings` 留有 `m_Settings != null` 断言。各行为组已完成并写入PASS，源包保留日志；没有把此事归零或用它证明功能失败。实际开发包probe未记录Error/Exception/Assert。此编辑器清理问题和教学离开分支的专项覆盖不足均列为已知限制，不扩大本轮改动。

## 原文件保护及复核

原始完整备份在工程同级 `LastCellOfPower-polish-baseline-20261114-194356`。既有 Assets/Packages/ProjectSettings 共1617文件，最终1613字节一致，四个授权变更是 EscapeInteraction、NoiseDevice、TacticsDevices、ChapterPlayerUI；没有缺失原文件。后续新目录及DeviceFeedback为新增。固定源场景依赖清单曾只重绑已授权的UI修订，原清单与变更解释保留，没有整体刷新来掩盖差异。

并行只读复核未发现可复现P1/P2；复核者参与过检查编写，故不称完全独立第三方验收。最终工程状态由主Agent重新读取报告、查看图像、重新执行受影响verify/route/build并核对文件绑定。

## 集中试玩尚待观察

最终任务为自由完成逃生，不要求使用全部工具。能否识别远端声源、避免把“曾响应”当作安全保证、理解门取消原因与次数、主动选择Q/诱导/封门，需要用户实际体验。没遇到的情境标未验证；没有捏造玩家反馈。本轮至同一版交付为止，不继续添加内容或调整数值。
