# 最后一格电：废弃工业仓库写实美术版

本版落实了后续的两个方向：环境向《生化危机2／3重制版》废弃工业设施的材质与照明方向靠近；追逐机器人换为生锈、破损的工业巡检机。使用原创和CC0素材，未提取参考游戏的模型、纹理或标识。镜头和潜行规则继续保持本工程原有的第一人称设计。

## 打开与操作

解压Windows包后运行 `LastCellOfPower.exe`。WASD／方向键移动，鼠标观察，按住E交互，Q应急干扰，Esc暂停，结局后R重开。玩家机器人的固定检修灯负责近处细节可读性，不增加按键、资源消耗或敌人感知规则。

Unity场景为 `Assets/ChapterOneArt/Scenes/WarehouseChapterOneArtRealism.unity`。主要配置为 `Assets/ChapterOneArt/Config/ChapterRealismProfile.asset`。原ChapterOne、WarehouseExpanded与单房场景保留；初版美术场景也保留，当前版本以带Realism后缀的场景为准。

## 本版改造

仓库补齐屋顶、钢梁、吊挂灯与管线；墙面改为受潮混凝土和剥落旧漆，四区加入不同设备与导向细节。三套带配套PBR纹理的CC0物件被组合为17个箱体／货架实例；军用箱只采用闭合变体，并按实际支撑高度堆放。连续后板、侧板及装载物保留原有遮挡，新增视觉没有碰撞或导航障碍。

机器人采用原创独立FBX，包含护板缺口、凹陷与弯折、外露线缆、散热格栅、履带和轮毂。新视觉限定在原机器人的外轮廓范围内，原NavMeshAgent、碰撞、感知眼高和速度保持不动。扫描窄条只读取巡逻／警觉／追击／干扰状态，提供相应颜色。

照明使用固定曝光和局部工作光。自定义间接补光在渲染后核对实际SH系数；四个房间反射保存为独立128分辨率HDR资产，不依赖后台模式中未实际更新的实时探针。湿地面遮罩同时衰减镜面反射，避免硬边多边形。旧漆和锈层按非金属为主处理，避免把整台机器人压成黑色镜面。

## 源码与素材复现

源码包包含 Assets、Packages、ProjectSettings、Documents、Tools、Evidence 和 SourceArt，不包含Library缓存。Blender源文件和生成脚本随包提供。素材来源、作者、许可、上游MD5与SHA-256分别记录于 `Sources.json`、`RealismSources.json`、各模型的 `SOURCE.json` 和 `RustGuard-source.json`。

```powershell
python Tools/PrepareChapterArtTextures.py
python Tools/PrepareRealismSurfaces.py
python Tools/PrepareRealismModels.py
```

`Tools/ConvertRealismModels.py` 通过Blender后台运行，使用 `--factory-startup --disable-autoexec`，只导出指定Mesh、配套贴图和材质记录。`Tools/BuildRustGuard.py` 生成原创机器人，拒绝覆盖已有输出。已有素材文件会按记录校验，不静默替换来源。

```powershell
unity run . --command 'art generate' --log-file Evidence/ChapterOneArtRealism/generate.log --no-tail --timeout 900 --format json
unity run . --command 'art bake_reflections' --log-file Evidence/ChapterOneArtRealism/bake-reflections.log --no-tail --timeout 600 --format json
unity run . --command 'art verify' --log-file Evidence/ChapterOneArtRealism/verify.log --no-tail --timeout 360 --format json
unity run . --command 'art build' --log-file Evidence/ChapterOneArtRealism/build.log --no-tail --timeout 1200 --format json
```

`art generate` 不覆盖已有场景；开发修订使用 `art regenerate`，先保存候选备份。修改场景美术后重新执行 `art bake_reflections`，以更新静态房间反射。`art finish_realism` 用于本轮已确认的货物修补及反射重建，不修改旧关卡物理数据。

## 证据边界

后台路线使用真实控制器和合成输入，没有设置玩家或敌人坐标或调用Warp；固定机位图是摆拍对比。Windows开发包的显式 `-art-render-probe` 会实际渲染1920×1080画面，并用同步GPU读回确保每一帧确实完成绘制，测量包含同步开销；它不等同于长时间正常游玩帧率。正式结果及已知边界见同包 `Acceptance.md` 与 `Performance.json`。
