# 最后一格电：双机器人战术版

本版保留24×24四区仓库、玩家3.4m/s、巡逻1.5m/s、追击3.0m/s和供电逃生目标，加入第二台独立机器人、两处远程声源和一扇临时隔离门。两台各有编号、状态灯、方向提示和空间声音；Q自动选择四米内无遮挡的最近有效威胁，一次扣12电、只停机一台。

当前Windows开发包：工程同级 `Builds/ChapterTactics-20261113-221423`，构建0错误0警告。三条控制器路线和双威胁呈现检查通过。最终渲染采样平均5.435ms/P956.266ms，完整边界见Acceptance.md。

## 操作与选择

WASD移动，鼠标观察，E充电／供电；对准环境按钮新按下E立即激活，操作不消耗电量。Q干扰，Esc暂停，结局后R重开。

设备区与货架区的诱导器各可使用一次，声源持续六秒，范围采用可达导航路径十二米；无人响应也消耗次数。目视追击优先，失去视线后仍在持续的声源可以转移调查。每敌每事件只响应一次，调查最多十秒、到达观察最多两秒。

东连接口隔离门默认打开，两侧按钮共用一次成功闭合机会。关闭0.35秒、闭锁4秒、打开0.35秒。门口占用会拒绝操作；关门途中有人进入会取消回开并保留次数。原四区绕行仍存在，所有装置均为可选手段。

## 工程与复现

新场景 `Assets/ChapterTactics/Scenes/WarehouseTactics.unity`，配置和导航分别位于其Config、Navigation目录。旧单敌场景保留，新增敌人集合为空时使用旧enemy引用。源码、已有CC0与原创美术、字体许可一起交付。

```powershell
unity run . --command 'tactics generate' --log-file Evidence/ChapterTactics/generate.log --no-tail --timeout 600 --format json
unity run . --command 'tactics verify' --log-file Evidence/ChapterTactics/verify.log --no-tail --timeout 420 --format json
unity run . --command 'tactics routes' --log-file Evidence/ChapterTactics/routes.log --no-tail --timeout 600 --format json
unity run . --command 'tactics build' --log-file Evidence/ChapterTactics/build.log --no-tail --timeout 900 --format json
```

生成器拒绝覆盖已有候选。图形探针使用 `-tactics-render-probe -tactics-evidence-dir=绝对路径`，显式绘制1920×1080并同步读回；不使用-nographics。该计时包含同步开销，不等同于正常长时间游玩FPS。

验收状态以同包Acceptance.md为准。五至八分钟是初次体验目标，不设置最低通关时长，也不以脚本用时冒充人的首通时长。
