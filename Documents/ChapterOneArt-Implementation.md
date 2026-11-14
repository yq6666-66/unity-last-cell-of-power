# 长期废弃仓库美术版

## 目标与边界

本版沿用 ChapterOne 已验收的四区地图、资源、脉冲干扰与敌人行为。新增结构、表面、照明、区域细节及可选界面主题。所有原始场景、素材与导航数据保留；新场景为 `Assets/ChapterOneArt/Scenes/WarehouseChapterOneArt.unity`。

新增美术视觉根 `ArtWorld` 与可单独隐藏的 `ArtRoof`；装饰无玩法 Collider 或 NavMeshObstacle。设备细节随原设备父级，门叶细节随原门叶。生成器逐项比较 Collider、Obstacle、导航资产、玩家、敌人、相机、交互锚点与配置，差异即停止交付。

## 资源与配置

`ChapterArtProfile` 保存八类材质、灯光、独立 Volume、可选 UI 配色、轻微装饰灯波动与固定种子。`ChapterPlayerUI` 和 `ChapterVisualFeedback` 的 `artProfile` 为可选项，空值保留旧场景效果。

屋顶底面约4.2米，工作灯约3.3米。最多四盏局部灯投影，其他灯为不投影补光；相机启用 URP 后处理与 SMAA，固定曝光、ACES、轻量 Bloom 与暗角。原眼高、视野、导航和捕获规则不受光线影响。

`Tools/PrepareChapterArtTextures.py` 获取五套 Poly Haven CC0 PBR 素材，校验上游 MD5，再记录 SHA-256；其余三类为确定性原创生成。URP Packed 为 R=Metallic、G=AO、A=1-Roughness，线性导入。颜色使用 sRGB，法线采用 GL normal map 并按 NormalMap 导入。原始 ARM 字节随源码保留，转换方式、来源、作者、许可与哈希见 `Assets/ChapterOneArt/Sources.json`。独立 ArtChinese 字体图集复用已有 OFL 字体源，避免新增铭牌文字改动原图集。

## 可复现入口

在项目目录执行，命令由已安装的 Unity Pipeline 注册：

```powershell
python Tools/PrepareChapterArtTextures.py
unity run . --command 'art generate' --log-file Evidence/ChapterOneArt/generate.log --no-tail --timeout 600 --format json
unity run . --command 'art capture' --log-file Evidence/ChapterOneArt/capture.log --no-tail --timeout 300 --format json
unity run . --command 'art verify' --log-file Evidence/ChapterOneArt/verify.log --no-tail --timeout 360 --format json
unity run . --command 'art build' --log-file Evidence/ChapterOneArt/build.log --no-tail --timeout 600 --format json
```

`art generate` 拒绝覆盖已有场景。开发中明确调用 `art regenerate` 会先保存候选场景与配置备份，然后从原 ChapterOne 基线重建。已交付包不作为输出目标。

新 Windows 开发包提供显式 `-art-render-probe` 入口，配合 `-art-evidence-dir=<绝对路径>` 输出实际图形设备上的 1920×1080 图像、600帧采样和性能报告；禁止使用 `-nographics`。该模式是固定机位渲染探针，不代表完整玩家路线或主观试玩。正常启动不会安装该探针。

## 验证与交付

集中验证包含几何快照一致、配置/材质/后处理引用、两盏装饰灯与尘粒暂停生命周期、无脉冲/一次脉冲的实际控制器行走路线以及重开复位。截图摆拍与路线证据分开。固定机位覆盖出生、四个连接口、终端、出口、敌人警觉/追击、三种 UI 分辨率及无后处理对照。

Windows包目标是在 RTX4060 Laptop GPU、100%渲染比例、1920×1080下平均至少60FPS且P95帧耗时不超过20ms。整体帧间隔和可获得的GPU计时分别报告，不能互相冒充。最终状态以 `Evidence/ChapterOneArt/Acceptance.md` 为准。
