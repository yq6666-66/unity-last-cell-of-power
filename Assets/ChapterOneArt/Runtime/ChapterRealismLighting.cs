using UnityEngine;
using UnityEngine.Rendering;

namespace LastCellOfPower.ChapterOneArt
{
    // Scene-local diffuse bounce and one-time box-projected reflections for physical surfaces.
    public sealed class ChapterRealismLighting : MonoBehaviour
    {
        public ChapterArtProfile profile;
        SphericalHarmonicsL2 previous;
        AmbientMode previousMode;
        void OnEnable()
        {
            previous=RenderSettings.ambientProbe;
            previousMode=RenderSettings.ambientMode;
            Apply();RenderPipelineManager.beginCameraRendering+=BeginCamera;
        }
        public SphericalHarmonicsL2 ExpectedProbe(){var sh=new SphericalHarmonicsL2();sh.AddAmbientLight(profile?profile.diffuseBounce:new Color(.19f,.205f,.21f));return sh;}
        void Apply(){RenderSettings.ambientMode=AmbientMode.Custom;RenderSettings.ambientProbe=ExpectedProbe();}
        void LateUpdate(){Apply();}
        void BeginCamera(ScriptableRenderContext context,Camera camera){Apply();}
        void OnDisable(){RenderPipelineManager.beginCameraRendering-=BeginCamera;RenderSettings.ambientMode=previousMode;RenderSettings.ambientProbe=previous;}
    }
}
