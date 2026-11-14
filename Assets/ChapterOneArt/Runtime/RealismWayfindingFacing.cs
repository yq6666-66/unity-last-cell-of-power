using UnityEngine;
namespace LastCellOfPower.ChapterOneArt
{
    // A wall-mounted label is readable from its front, never mirrored through its back.
    public sealed class RealismWayfindingFacing : MonoBehaviour
    {
        public Camera viewCamera;
        Canvas sign;
        void Awake(){sign=GetComponent<Canvas>();}
        void LateUpdate()
        {
            if(sign&&viewCamera)sign.enabled=Vector3.Dot(-transform.forward,viewCamera.transform.position-transform.position)>.01f;
        }
    }
}
