# 09-技术文档

状态：整体方案参考；本次迭代不重复逐项确认。以下技术合同已用于本轮实施，实际执行和未验证范围以本轮Acceptance及交付说明为准。

NoiseDevice.ResponseCount只读；内部按guardId去重，ResetState清除。当且仅当ExpandedEnemy.TryInvestigateNoise返回成功，TacticsDevices记录该设备的实际接纳并发出NoiseResponseAccepted。信号结束保留历史响应数至重开，不改AI LastSeen或调查目标。

DeviceFeedback采用明确类型，携带Sequence、Frame、DeviceId、NoiseEventId、ResponseCount、Consumed、Kind。类型区分Noise激活/响应/结束/已用拒绝，以及门占用拒绝、忙碌、用尽、关闭开始、占用取消、闭合和打开。事件在相应状态提交后发布；失败分支也从实际规则产生，不让UI解析字符串猜原因。

PolishFeedbackPresenter持有单一短提示与独立教学的剩余时长。有效游玩时间推进，Pause/FocusLost冻结，Outcome与新局清暂态；较低优先级成功消息不能替换尚未结束的拒绝/取消。Sequence用于阻止同事件重放，重新启用组件同步当前状态但不重播旧LastFeedback。

DeviceReadabilityView从真实NoiseDevice或TacticalShutter读取状态，使用文字和独立PropertyBlock更新灯色，禁用时恢复原呈现值。TacticalReadabilityProfile集中编号目的地、文案、显示时长和配色；未配置的旧UI沿用原分支。

生成器复制WallFix场景，新建呈现配置和中文字体，预热全部实际文字；原Gameplay/Level/Pulse/Devices配置、NavMesh与修墙Mesh保持原引用。源与新场景稳定快照只允许新标识根、UI呈现/font绑定及开发诊断开关的差异。构建与证据须对应同一候选版本，旧包和基线不覆盖。
