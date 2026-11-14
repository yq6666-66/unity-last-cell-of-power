# 战术信息反馈版生成与保护入口

状态：独立 candidate 已生成。紧凑世界牌修复后的 `polish inspect` 与 `polish presentation` 已通过；本次没有重新运行已通过的 route/verify，也没有构建开发包，最终构建由主 Agent 审图后执行。

本批只读核对并固定参考仓库 `yq6666-66/game-development-plugins` main 提交 `8693e66d50f40b70f95fec3f77f3a0322ef08eb3`（恢复批次；原 `becd721c27c01ef571b3c6a9886019593291bea5` 记录为历史）。生成器作者此前读取 `skills/game-visual-iteration/SKILL.md` 和 `skills/game-playloop-validation/SKILL.md`，采用固定原布局与渲染配置、颜色加文字、呈现只读状态、实际几何间距与源资产保护。主 Agent 已补读输入文字、恢复及声音信息引用。不适用引擎迁移、安装、付费、地图重建、平衡调参或发布。

| 入口 | 行为 |
| --- | --- |
| `polish generate` | 复制 WallSurfaceFix 场景，生成独立反馈 profile、中文 font 和 6 组状态牌，保存新场景后调用 inspect。再次调用时仅 inspect 已有候选，不覆盖场景。部分候选失败需先保留再明确重试。 |
| `polish repair_signs` | 备份已有 candidate，仅把新增六牌改为紧凑布局与短状态，保留真实设备锚点及物理规则，最后调用 inspect。 |
| `polish inspect` | 比较原对象/Transform/Collider/Nav/AI/电量/机关规则/修墙 mesh/灯/相机/post，校验匹配设备绑定、文字预热和新标识共面交叠。 |
| `polish build` | 先 inspect，再构建新的 Windows Development 包到 `../Builds/ChapterTacticsPolish-时间戳`。 |
| `polish probe` | 仅打印下方开发包显式 probe 使用指引，不启动玩家进程。 |

场景：`Assets/ChapterTacticsPolish/Scenes/WarehouseTacticsPolish.unity`。

证据：`Evidence/ChapterTacticsPolish`。检查输出包括保护快照、原修墙 mesh 引用、源依赖生成前后 SHA-256、候选依赖 SHA-256 和保护摘要。保护允许项只有可选 UI profile/presenter/font 绑定与 diagnosticsEnabled；新增标识单独放在 `TacticsPolishReadability` 根物体，不增 Collider、NavMeshObstacle 或 Light。

标识为 N01/N02 控制台和对应扬声器各一组，S01 门两侧各一组。每组由 Plate、Title、Destination、State、Lamp 组成，当前牌面为 0.62×0.34 m，完整目的地分两行，世界牌仅显示短状态和工作灯；倒计时、历史响应与次数保留在真实 HUD。控制牌位于既有锚点上方 0.27 m，扬声器牌位于声源锚点上方 0.50 m，仅新增装饰变换调整。文字距底板前表面 34.5 mm。工作灯沿用独立中性灰 emission 材质及 URP BakedEmissive 标志，运行时由真实状态着色。所有新牌无 Collider、NavMeshObstacle 或 Light；实际三角面共面交叠检查通过。

原失败的 0.85 m 近距、1.2 m 与 1.8 m 同方向视角已保留相同眼高/FOV复图，三个位置的编号/目的地/状态实际 glyph bounds 均完全落在视口内。原大牌截图保留在 `Attempts/PresentationBeforeCompact`。菜单主卡在 Polish 配置下改为不透明，1280 实际截图未再见世界文字透出。上述是受控呈现证据，不替代正常玩家操作或主观理解验收。

原 source manifest 保存在 `source-assets-first-generation.sha256`；本次仅按已批准的菜单修复重绑 `ChapterPlayerUI.cs` 一行，其他 1153 项源依赖哈希未变。差异见 `source-ui-authorized-rebind.json`，没有整体刷新资产基线。

开发包性能检查复用现有 `TacticsBuildProbe`，不复制运行时探针：

```text
LastCellOfPower.exe -batchmode -tactics-render-probe -tactics-evidence-dir="<Evidence/ChapterTacticsPolish/PlayerProbe绝对路径>" -logFile "<绝对日志路径>"
```

必须保留 graphics，不加 `-nographics`。显式标志以外的正常启动不安装探针。探针使用 60 帧预热及 600 帧 1920×1080 Camera.Render 和同步回读，结果仅表示受控渲染吞吐，不表示正常游玩 FPS、玩家理解或首通时长。主逻辑、呈现和路线夹具分别由 `PolishChecks` 的 `polish verify/presentation/route` 负责。
