# 02-环境与构建

状态：用户于2026-11-14明确回复“确认02-环境与构建”，本项已确认。后续用户澄清九项流程只用于首次项目，并恢复本轮active goal；当前实施授权来自完整既定目标及最新指令，非本项单独确认。

沿用当前Windows工程、Unity6000.3.28f1、URP17.3.0、Input System1.20.1、AI Navigation2.0.16、uGUI2.0.0所带TMP和现有Noto字体。2026-11-14已只读核对ProjectVersion、manifest与packages-lock；不因参考案例升级引擎、替换管线或安装工具。

源场景为Assets/WallSurfaceFix/Scenes/WarehouseTacticsWallFix.unity；新场景拟为Assets/ChapterTacticsPolish/Scenes/WarehouseTacticsPolish.unity。新配置、字体与标识另存；原玩法配置、导航和修复网格保持引用及数值。

开工前先核对参考仓库main提交与所需流程，记录完整SHA。Editor入口包括polish generate/inspect/build，检查入口包括polish verify/presentation/route。初稿时这些入口尚未编译；恢复后已实际执行并保留日志，最终状态见本轮Acceptance，不能只把命令存在当作检查通过。

构建独立Windows Development包，输出新时间戳目录，不覆盖原包。运行时图形探针沿用现有显式开启的TacticsBuildProbe；保留真实graphics，记录实际设备、分辨率与采样条件，测量与正常玩家表现分开。源码包包含必要Assets/Packages/ProjectSettings、工具、文档和本轮证据，不包含Library缓存及无关历史大体积帧序列。
