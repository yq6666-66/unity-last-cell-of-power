# 墙体移动闪烁修复版

当前Windows包：工程同级 `Builds/WallSurfaceFix-20261114-002336`，运行其中LastCellOfPower.exe。完整Windows压缩包可直接解压运行，无需在旧包中替换文件。

本版清除连接口门框与墙皮、北出口门框与墙端、护栏端盖与北墙，以及临时门导轨与旧门框的重叠可见面。墙脚/漆边改为相邻材质带，保留原有材质、尺寸和全部玩法。新场景 `Assets/WallSurfaceFix/Scenes/WarehouseTacticsWallFix.unity`，48个独立网格与4个独立反射资产另存；旧场景和原网格未覆盖。

## 对照影片

Before.mp4、After.mp4各12秒，1280×720、30fps、360帧，相同相机轨迹/FOV。Comparison.mp4左侧为修复前，右侧为修复后。前6秒覆盖北连接口的前进、后退、横移、转头，随后覆盖其余连接口和出口。玩法被冻结以稳定画面对照，灯光、阴影、SSAO、后处理保留；这些影片不是正常玩家操作录像。

## 源码补丁

WallFix-SourcePatch.zip不是独立完整工程，应应用到已交付的ChapterTactics双机器人源码基线（ChapterTactics-Source.zip SHA256为2596e219bd12dbf485b5244438e31b8dfb63ed2a8d937504b8506b233afdff0a）。先备份，再按补丁目录合并；补丁包括WallSurfaceFix新资产、两处生成器接入源码、文档和检查摘要。不包含Library缓存或数百MB的逐属性原始快照；完整原始快照保留在工作区Evidence/WallSurfaceFix。

```powershell
unity run . --command 'wallfix generate' --log-file Evidence/WallSurfaceFix/generate.log --no-tail --timeout 900 --format json
unity run . --command 'wallfix verify' --log-file Evidence/WallSurfaceFix/verify.log --no-tail --timeout 300 --format json
unity run . --command 'wallfix idempotence' --log-file Evidence/WallSurfaceFix/idempotence.log --no-tail --timeout 300 --format json
unity run . --command 'wallfix motion' --log-file Evidence/WallSurfaceFix/motion.log --no-tail --timeout 600 --format json
unity run . --command 'wallfix build' --log-file Evidence/WallSurfaceFix/build.log --no-tail --timeout 900 --format json
```

生成器拒绝覆盖已有修复场景；已有场景使用verify和不保存场景的idempotence入口检查。后续美术生成与战术生成已接入同一修补规则，避免重新生成时复发。
