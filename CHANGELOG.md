# Changelog

## Unreleased

### 战术信息与操作反馈完善版

以墙面修复场景另存ChapterTacticsPolish。新增N01/N02控制台与远端扬声器配对、紧凑状态牌及真实去重响应历史；S01明确占用拒绝、关闭取消和次数保留。短提示按成功2秒/失败3秒去重与优先级显示，暂停失焦冻结，重开清旧反馈；增加安全接近轻教学和诱导/门/Q三规则卡。原两敌、速度、电量、次数、碰撞导航和供电逃生目标不变。

最终10组逻辑、2组呈现、1条连续合成输入路线通过；新Windows开发包ChapterTacticsPolish-20261114-223021为0错误0警告。1920×1080受控渲染加同步600帧平均5.240ms/P956.087ms，探针退出0。普通交付副本启动就绪；桌面工具不可用，普通点击和主观理解留集中试玩。编辑器检查清理阶段InputSystem断言原样记录，不宣称日志全净；全部证据与边界见本轮Acceptance。每批继续核对参考仓库，九项确认仅首次开工/接手。

### 双机器人战术版

新增独立ChapterTactics场景、两机器人分区巡逻、两处单次免费远程诱导和一次临时隔离门。速度与供电逃生目标不变；Q按最近有效威胁选择单目标，双威胁HUD和音源分别绑定。关门占用拒绝、Closing进入取消不耗次数、完全闭合后4秒自动释放，保留四区绕路与敌人记忆。原单敌场景通过兼容检查。

三条无位置修改控制器路线、规则合并检查、双威胁呈现检查及Windows构建均通过。开发包ChapterTactics-20261113-221423，0错误0警告；显式1080p渲染同步采样600帧平均5.435ms/P956.266ms，不冒充正常游玩帧率或首通时长。

### 写实废弃仓库与锈损巡检机交付

按后续美术方向继续完成Realism独立场景：三套CC0模型组合为17个真实货物实例，受潮混凝土与旧漆墙面、圆管法兰、四区细节、离线HDR房间反射和固定近处检修照明。巡逻机换为原创8渲染器破损模型，增加锈斑、缺口护板、线缆、轮毂履带与只读状态扫描条。修复货物变体混入、幕布遮挡、支撑高度、透明湿片硬边、环境光被覆盖和后台实时反射未生成等问题。

最终Windows开发包为ChapterOneArt-20261113-164659，0错误0警告。R2合并检查与两条后台通关路线通过；最终1080p渲染加同步采样600帧平均4.977ms、P95 5.589ms，独立GPU计时不可用。原371个受保护文件仅三个授权共用脚本改变，其余368个字节保持一致。全部当前结果与边界以Evidence/ChapterOneArtRealism/Acceptance.md为准。

### ChapterOneArt 废弃仓库美术版

新增独立美术场景，复用已验收的四区布局、碰撞、导航与玩法参数。补齐4.2米屋顶、梁架、吊灯与管线；引入八类PBR表面，完成封闭货物、设备和门叶细节、旧铭牌与四区环境装饰。材质及字体图集独立保存，外部CC0素材附作者、许可、原始校验和与SHA-256；生成脚本固定已记录的下载地址与校验。

新增可选美术主题，旧场景默认分支保留；装饰灯与尘粒随暂停冻结。修复URP验证清除发光关键字的问题，并在保存重导后验证。美术生成与构建前核对玩法快照，隔离TMP默认字体自动格式升级。两条无坐标修改的路线均通过，Windows开发包0错误0警告；实际图形性能与源包哈希见本版Acceptance及交付清单。固定机位与合成输入证据不代替用户主观试玩。

### ChapterOne 完整第一关交付

交付独立ChapterOne-20261113-011649 Windows开发包，0错误0警告并通过后台启动。有效Q事务、停机恢复、菜单输入门控、中文与正式HUD、整机高亮、原创新音效、统计与结算已接入；修复Filled条缺少Sprite导致满条显示的问题。两条不改位置的后台路线分别验证不用Q和使用一次Q后继续逃生，重载清理通过。字体及TMP资源原始许可随包保留，主观音量与手感留给同一版集中试玩。


### ChapterOne 完整第一关

新增独立ChapterOne场景与配置，保留已验收四区布局和宽松数值。Q仅对4米内无遮挡的Alert/Chase威胁生效，门槛13、费用12、停机2.5秒、冷却15秒；失败请求不扣费，成功才中断E。敌人停机期间不更新感知，恢复原决策阶段，不额外清空已确认警觉。旧场景默认不开启新能力。

玩家界面改为uGUI＋TMP，明确开局/运行/暂停/设置/帮助/结果模态，滑条或任意点击不会恢复游戏。新增轻引导、四区编号导向、完整设备Renderer高亮、敌人状态灯、核心音效、主音量与灵敏度设置。结算冻结有效时间、余电与成功干扰次数，无效Q不计数，保留快速重开。

引入固定提交的Noto Sans CJK SC Regular与OFL许可，十三个音效由项目脚本原创确定性合成。仅导入已安装uGUI包的TMP Essentials，并保留LiberationSans OFL与EmojiOne署名；未导入参考项目框架或Examples/Extras。修复TMP异步导入准备、Filled Image缺sprite以及高亮仅作用marker的问题。

核心与presentation-r2后台检查通过；离屏中文与布局已核对。不使用Q与使用一次Q的合成输入路线分别行走33.316/66.224米，余电15.861/13.818，均进入Results且没有Warp或位置设置；重载清理断言通过。Windows构建及最终包路径待本轮收尾登记，以上不冒充桌面人手或主观听感验收。

历史条目保留以下，描述各自阶段，不改写为当前第一关状态。


### 四区宽松版交付

完成24×24四区环路、可配置巡逻和Alert、1.2秒警觉与0.35秒捕获确认、最多6秒搜索、移动取消交互、较低电量消耗和局部出口。完成两处转角净宽调整，保留旧场景默认行为。后台合并逻辑、实际控制器行走充电供电逃生路线、三结局重开及Windows包启动通过；Windows构建0错误0警告，目录WarehouseExpanded-20261112-220355。路线检查为合成输入，不冒充桌面人手验收。开发过程中未要求用户逐项切窗。


### 24×24 四区仓库与宽松潜行（2026-11-12，开发中）

新增独立WarehouseExpanded场景、关卡定义与宽松玩法配置。复用已有工业风模块形成四区环路、三个充电点、西北供电终端与东北出口；拆开视觉、碰撞、锚点与导航，不整体拉伸旧仓库。生成器支持新场景输出，旧场景和开发包保留。

玩家速度3.4m/s，敌人巡逻1.5、追击3.0；新增约1.2秒警觉、遮挡消退、0.65米内持续0.35秒的捕获确认，以及总计最多约6秒的最后目击点附近搜索。巡逻节点数量可配置，临时导航失败做有时限恢复，不用隐藏玩家位置或瞬移补救。

新配置初始40、基础消耗0.2/秒、实际移动额外0.3/秒、净充电7.8/秒；供电2秒扣35，门槛36。新地图开启移动键立即取消交互，出口改用可选局部锚点。旧场景默认配置和旧交互行为保留。HUD补充区域、警觉与有限追击方向提示，不新增常驻小地图。

四个连接口和两条路线的静态导航检查已通过，路线约15.97/39.50米。几何角点与集中逻辑夹具仍在整合，尚无本轮最终Windows包或整体通过结论。按用户要求采用后台批次检查和最后一版集中试玩，不再逐项要求用户窗口验收。旧EscapeSample未完成的实际操作项目继续作为历史待验保留。

### 单房电量与逃生样本（2026-11-12）

新增独立 EscapeSample 场景与资源、交互、门、会话组件。电量上限100、初始35，基础耗电1/秒、实际移动额外0.5/秒；两充电座按住E每秒补8、净增7。交互范围1.8米，交互时水平锁定但可转镜头，暂停或失焦取消并要求释放E再恢复。

北门终端开始供电需48，连续3秒后一次扣45；中断进度清零，不返还已发生的基础耗电。门滑开1秒后才释放碰撞和动态导航障碍，进入指定门外区域且z≥6.8判胜。支持被抓、耗尽、逃生三结局和R重开，同帧优先级为错误、被抓、耗尽、逃生。

新场景按实际世界坐标调整门、终端及碰撞。修复被删除北墙对象仍在Transform缓存导致的接线异常；把堵住门洞的两护柱及碰撞移至x=-0.4和2.4，重建独立导航，保留旧场景与NavMesh。旧玩家仅增加默认关闭的水平移动锁和实际位移观察，原输入中性门合成回归通过。

资源、交互、场景自动检查通过；Windows开发包EscapeSample-20261111-231429构建成功，0错误0警告。授权Windows输入事件已验证充电、供电开门逃生、被抓与重开、自然耗尽与重开和供电中断。充电时被抓、新单元真实切窗取消/恢复及遮挡搜索返回等回归仍待实测，当前不声明整体PASS。自动夹具与实际输入证据分开保留。


### 第一人称单房潜行样本（2026-11-11）

新增独立 StealthSample 场景及玩家控制、感知、五态敌人、会话控制组件。提供第一人称相对移动、鼠标捕获与释放、暂停、F1 调试、Patrol/Chase/Search/Return/Caught 和被抓后 R 重开。保留原 Unit1 场景与运行脚本。

Editor 初始化与暂停画面检查通过；坐标控制的 PlayMode 逻辑检查覆盖感知遮挡、状态转换、最后目击点、搜索暂停、返回导航和隔墙安全捕获。Windows 开发包构建成功，0 错误、0 警告；授权 Windows 输入事件驱动的开发包验收通过：四向与斜向移动、四壁碰撞、F1、鼠标转向与俯仰限制、追击搜索返回、三次 R 重开、真实窗口切换后点击恢复及恢复静止。此证据不是人手实体键盘或手机测试。

修复恢复输入时初始状态检查延迟导致的中性门提前解除；增加合成输入回归与三次脚本重载、缺失引用停止检查，均已通过。修复版构建为 StealthSample-20261111-220613。

纠正文档中的旧 D 盘路径与未实现的 Game.unity 入口，记录旧 Unit1 证据边界及方向键输入未定因。本单元不增加电量、充电、胜利出口、两房流程或手机触控。

### Warehouse implementation unit 1

Added a separate WarehouseUnit1 scene using the approved refined warehouse asset, a fixed overhead camera, explicit collision geometry and baked patrol navigation. The original SampleScene and existing input asset are preserved.

Added movement using the existing Input System actions, normalized diagonal movement, four-point patrol with visible navigation errors, and an F1 diagnostic panel and path line. Native Windows development-package checks observed all four movement directions, an F1 off/on/off sequence, normalized diagonal displacement, an east-wall stop and intact child-renderer positions.

Added scene contract checks, a Windows Development Build command and opt-in development smoke/observation modes. Observation captures do not inject movement or change the diagnostic state. The old failed headless input diagnostic is not an acceptance pass.

Formal unit acceptance remains pending. An unexplained movement between the last two regression snapshots is retained for review; the final snapshot is excluded from scripted-movement conclusions. Actual focus recovery, failure injection, other walls and extended play remain unverified. This unit adds no chase, search, return, capture/reset, battery or exit gameplay.
