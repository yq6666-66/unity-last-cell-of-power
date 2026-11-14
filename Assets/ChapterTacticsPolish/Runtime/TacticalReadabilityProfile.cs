using System;
using UnityEngine;

namespace LastCellOfPower.ChapterTacticsPolish
{
    [CreateAssetMenu(menuName="Last Cell/Tactical Readability Profile")]
    public sealed class TacticalReadabilityProfile : ScriptableObject
    {
        [Serializable] public struct DeviceLabel { public string deviceId,shortId,destination; }
        public DeviceLabel[] noiseLabels={
            new DeviceLabel{deviceId="Noise_NW",shortId="N01",destination="设备区南侧扬声器"},
            new DeviceLabel{deviceId="Noise_SE",shortId="N02",destination="货架区南侧扬声器"}};
        public string shutterShortId="S01",shutterDestination="东侧通道";
        public float successToastSeconds=2,failureToastSeconds=3,tutorialSeconds=6,indicatorEmission=1.2f,tutorialApproachDistance=3;
        public Color readyColor=new Color(.28f,.66f,.49f),signallingColor=new Color(.92f,.61f,.18f),spentColor=new Color(.22f,.25f,.24f),rejectedColor=new Color(.95f,.38f,.21f);
        public string readyText="就绪 · 本局一次",signallingFormat="发声中 {0:0.0}s",spentText="已用尽";
        public string worldReadyText="就绪",worldSignallingText="发声中",worldClosingText="关闭中",worldClosedText="闭锁中",worldOpeningText="回开中";
        public string responsesFormat="曾响应 {0} 台",pendingResponseText="暂未收到响应",noResponseText="本次未收到响应";
        public string consumedText="本局次数已用",reservedText="次数保留",noiseReadyAction="按 E 启动 · 本局一次 · 不耗电";
        public string noiseActivatedFormat="{0} 已启动 → {1} · 发声{2:0.#}秒";
        public string noiseResponseFormat="{0} · 曾响应 {1} 台";
        public string noiseEndedFormat="{0} 发声结束 · {1}";
        public string noiseRejectedFormat="{0} 本局已用尽",noiseRejectedActiveFormat="{0} 正在发声 · 本局次数已用";
        public string shutterRejectedOccupiedFormat="{0} 门口有人，无法关闭 · 次数保留";
        public string shutterRejectedUsedFormat="{0} 本局已用尽 · 本次未再次消耗";
        public string shutterRejectedBusyFormat="{0} 动作进行中 · {1}";
        public string shutterClosingFormat="{0} 正在关闭 · 完全闭合才消耗次数";
        public string shutterCancelledFormat="{0} 关门取消：通道被占用 · 次数保留";
        public string shutterClosedFormat="{0} 已闭合 · 本局次数已用";
        public string shutterOpenedFormat="{0} 已打开 · {1}";
        public string closingState="关闭中 · 次数保留",closedStateFormat="闭锁中 {0:0.0}s",openingUsedState="开启中 · 次数已用",openingReservedState="正在回开 · 次数保留",openSpentState="已用尽 · 门已打开";
        public string noiseTutorial="按 E 启动同号扬声器，每台本局一次。\n看到你的机器人仍优先追你。";
        public string shutterTutorial="两侧开关共用一扇门。完全闭合才消耗次数；门口有人会拒绝或取消。";
        public string helpQTitle="Q · 单目标干扰";
        public string helpQBody="距离≤{0:0.#}米且无遮挡，仅作用于警觉/追击中的最近目标。\n需{1:0}电量，命中耗{2:0}；一次只干扰一台。";
        public string helpNoiseTitle="N01 / N02 · 诱导扬声器";
        public string helpNoiseBody="同号扬声器发声{0:0.#}秒，每台一次、不耗电。\n看到你的机器人仍优先追你；曾响应表示接受过调查。";
        public string helpShutterTitle="S01 · 临时隔断门";
        public string helpShutterBody="两侧 S01 共用一扇门，完全闭合才消耗次数。\n占位会拒绝或取消，次数保留；闭锁{0:0.#}秒后回开。";
        public string helpControls="WASD 移动 · 鼠标观察 · E 设备 · Esc 返回";
        public const string PreloadText="N01 N02 S01 0123456789 .:/≤→ Q E WASD Esc 就绪本局一次发声中已用尽曾响应台暂未收到响应本次未收到响应次数已用次数保留按启动不耗电设备区南侧货架区南侧东侧通道门口有人无法关闭未再次消耗动作进行完全闭合才消耗已取消正在回开已闭合已打开开启闭锁同号操作台与扬声器配对让目的地供你调整路线两侧开关共用一扇拒绝单目标干扰距离且无遮挡仅作用于警觉追击中的最近目标需电量命中耗一次只干扰一台只表示已接受调查占位会拒绝或取消后秒回开看到你的机器人仍优先追你通道被占用移动鼠标观察返回。·，；：（）“”";
        public DeviceLabel LabelFor(string deviceId)
        {
            if(noiseLabels!=null)foreach(var label in noiseLabels)if(label.deviceId==deviceId || label.shortId==deviceId)return label;
            return new DeviceLabel{deviceId=deviceId,shortId=deviceId,destination="扬声器"};
        }
        public string ResponseText(int count,bool finished)=>count>0?string.Format(responsesFormat,count):finished?noResponseText:pendingResponseText;
    }
}
