using UnityEngine;
using UnityEngine.Rendering;

namespace LastCellOfPower.ChapterOneArt
{
    [CreateAssetMenu(menuName="Last Cell/Chapter Art Profile")]
    public sealed class ChapterArtProfile : ScriptableObject
    {
        public Material concrete, paintedMetal, rustedMetal, wood, cardboard, fabric, rubber, grime;
        public Material mintEmission, warmEmission, coolEmission, glass;
        public VolumeProfile volume;
        public Color ambientColor = new Color(.35f,.37f,.40f);
        public float ambientIntensity = .9f;
        public Color diffuseBounce = new Color(.19f,.205f,.21f);
        public float reflectionIntensity = .75f;
        public int reflectionResolution = 128;
        public float inspectionLightIntensity = 4.5f;
        public float inspectionLightRange = 7f;
        public float mainLightIntensity = .16f;
        public float workLightIntensity = 3.4f;
        public Color warmLight = new Color(1f,.72f,.43f);
        public Color coolLight = new Color(.54f,.72f,1f);
        public Color fogColor = new Color(.038f,.047f,.053f);
        public float fogStart = 15f, fogEnd = 50f;
        public Color panelColor = new Color(.035f,.041f,.043f,.92f);
        public Color accentColor = new Color(.46f,.78f,.65f,1f);
        public float highlightStrength = .12f;
        public float flickerAmplitude = .11f;
        public float flickerPeriod = 4.7f;
        public int decorationSeed = 1132026;
    }
}
