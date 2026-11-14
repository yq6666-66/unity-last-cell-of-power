using System;
namespace LastCellOfPower.ChapterTactics
{
    public enum DeviceFeedbackKind
    {
        NoiseActivated, NoiseResponseAccepted, NoiseEnded, NoiseRejectedUsed,
        ShutterRejectedOccupied, ShutterRejectedUsed, ShutterRejectedBusy,
        ShutterClosing, ShutterCancelledOccupied, ShutterClosed, ShutterOpened
    }
    /// <summary>Committed observation only. Contains no actor location or navigation target.</summary>
    public readonly struct DeviceFeedback
    {
        public readonly int Sequence, Frame, NoiseEventId, ResponseCount;
        public readonly string DeviceId;
        public readonly bool Consumed;
        public readonly DeviceFeedbackKind Kind;
        public DeviceFeedback(int sequence,int frame,string deviceId,int noiseEventId,int responseCount,bool consumed,DeviceFeedbackKind kind)
        {Sequence=sequence;Frame=frame;DeviceId=deviceId;NoiseEventId=noiseEventId;ResponseCount=responseCount;Consumed=consumed;Kind=kind;}
    }
}
