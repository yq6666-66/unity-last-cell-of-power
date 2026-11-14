using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using LastCellOfPower.EscapeSample;
using LastCellOfPower.WarehouseExpanded;

namespace LastCellOfPower.ChapterOneArt
{
    /// <summary>Deterministic decorative effects; does not change gameplay light or AI state.</summary>
    public sealed class ChapterArtAtmosphere : MonoBehaviour
    {
        public ExpandedSession session;
        public ChapterArtProfile profile;
        public float Elapsed { get; private set; }
        public int FlickerLightCount => lamps.Count;
        public int DustEmitterCount => dust.Count;
        readonly List<Light> lamps=new List<Light>();
        readonly List<float> intensities=new List<float>();
        readonly List<ParticleSystem> dust=new List<ParticleSystem>();
        Material dustMaterial;
        Texture2D dustTexture;
        bool initialized;

        void Start(){Initialize();}
        void Update(){Step(Time.deltaTime);}

        public void Initialize()
        {
            if(initialized || !session || !profile)return;
            initialized=true;Elapsed=0;
            foreach(var light in FindObjectsByType<Light>(FindObjectsSortMode.None))
            {
                if(light.gameObject.scene!=gameObject.scene || (light.name!="ArtFlicker_0" && light.name!="ArtFlicker_1"))continue;
                lamps.Add(light);intensities.Add(light.intensity);
            }
            // Deterministic ordering, regardless of the editor's instance IDs.
            if(lamps.Count==2 && string.CompareOrdinal(lamps[0].name,lamps[1].name)>0)
            {
                var lamp=lamps[0];lamps[0]=lamps[1];lamps[1]=lamp;
                float intensity=intensities[0];intensities[0]=intensities[1];intensities[1]=intensity;
            }
            CreateDust();
        }

        public void Step(float deltaTime)
        {
            if(!initialized)Initialize();
            if(!initialized || !session.Ready || session.Paused || session.Outcome!=RunOutcome.Running || deltaTime<=0 || float.IsNaN(deltaTime) || float.IsInfinity(deltaTime))return;
            Elapsed+=deltaTime;
            float amplitude=Mathf.Clamp(profile.flickerAmplitude,0,.2f),period=Mathf.Max(3f,profile.flickerPeriod);
            for(int i=0;i<lamps.Count;i++)
            {
                if(!lamps[i])continue;
                float phase=Elapsed*(Mathf.PI*2/period)+i*2.13f;
                float variation=Mathf.Sin(phase)*.7f+Mathf.Sin(phase*.43f+i)*.3f;
                lamps[i].intensity=intensities[i]*(1+variation*amplitude);
            }
            // Manual simulation prevents particles from advancing during the
            // game's pause (which intentionally does not change Time.timeScale).
            foreach(var particles in dust)if(particles)particles.Simulate(deltaTime,false,false,false);
        }

        void CreateDust()
        {
            if(!profile.glass)return;
            dustTexture=new Texture2D(32,32,TextureFormat.RGBA32,false){name="ArtDustSoftDisc",hideFlags=HideFlags.DontSave,wrapMode=TextureWrapMode.Clamp};
            var pixels=new Color[32*32];
            for(int y=0;y<32;y++)for(int x=0;x<32;x++)
            {
                float radius=Vector2.Distance(new Vector2(x+.5f,y+.5f),new Vector2(16,16))/16;
                pixels[y*32+x]=new Color(1,1,1,Mathf.Pow(Mathf.Clamp01(1-radius),2));
            }
            dustTexture.SetPixels(pixels);dustTexture.Apply(false,true);
            // The glass asset supplies an included, transparent URP shader. Do
            // not depend on Shader.Find surviving player shader stripping.
            dustMaterial=new Material(profile.glass){name="ArtDustRuntime",hideFlags=HideFlags.DontSave};
            dustMaterial.SetTexture("_BaseMap",dustTexture);dustMaterial.SetColor("_BaseColor",new Color(.65f,.73f,.77f,.18f));
            dustMaterial.SetColor("_EmissionColor",new Color(.035f,.043f,.048f));dustMaterial.EnableKeyword("_EMISSION");
            dustMaterial.SetFloat("_Smoothness",0);dustMaterial.SetFloat("_Metallic",0);
            Vector3[] centers={new Vector3(-8,2.6f,-7),new Vector3(5,2.6f,-6),new Vector3(-7,2.6f,6),new Vector3(7,2.6f,7)};
            for(int i=0;i<centers.Length;i++)
            {
                var go=new GameObject("ArtDust_"+i);go.transform.SetParent(transform,false);go.transform.position=centers[i];
                var particles=go.AddComponent<ParticleSystem>();particles.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
                var main=particles.main;main.loop=true;main.playOnAwake=false;main.startLifetime=new ParticleSystem.MinMaxCurve(8,12);
                main.startSpeed=.018f;main.startSize=new ParticleSystem.MinMaxCurve(.008f,.018f);main.maxParticles=36;
                main.simulationSpace=ParticleSystemSimulationSpace.World;main.cullingMode=ParticleSystemCullingMode.AlwaysSimulate;
                main.startColor=new Color(.75f,.82f,.85f,.45f);
                var emission=particles.emission;emission.rateOverTime=2;
                var shape=particles.shape;shape.shapeType=ParticleSystemShapeType.Box;shape.scale=new Vector3(2.4f,1.4f,2.4f);
                var renderer=particles.GetComponent<ParticleSystemRenderer>();renderer.sharedMaterial=dustMaterial;
                renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;
                particles.useAutoRandomSeed=false;particles.randomSeed=unchecked((uint)(profile.decorationSeed+i*101));
                particles.Simulate(3,false,true,false);particles.Pause(false);dust.Add(particles);
            }
        }

        void OnDisable()
        {
            foreach(var particles in dust)if(particles)particles.Pause(false);
        }
        void OnDestroy()
        {
            for(int i=0;i<lamps.Count;i++)if(lamps[i])lamps[i].intensity=intensities[i];
            if(dustMaterial)Destroy(dustMaterial);
            if(dustTexture)Destroy(dustTexture);
        }
    }
}
