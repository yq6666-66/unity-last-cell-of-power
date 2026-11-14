using System;
using System.Collections.Generic;
using UnityEngine;
using LastCellOfPower.WarehouseExpanded;
using LastCellOfPower.EscapeSample;
using LastCellOfPower.ChapterOneArt;

namespace LastCellOfPower.ChapterOne
{
    public sealed class ChapterVisualFeedback : MonoBehaviour
    {
        [Serializable]
        public sealed class HighlightBinding
        {
            public EscapeInteractable target;
            public Renderer[] renderers = new Renderer[0];
        }
        [Serializable]
        public sealed class GuardLightBinding
        {
            public ExpandedEnemy guard;
            public Light statusLight;
        }

        public ExpandedSession session;
        public EmergencyPulseController pulse;
        public Light enemyStatusLight;
        public Material pulseMaterial;
        public ChapterArtProfile artProfile;
        public GuardLightBinding[] guardStatusLights=new GuardLightBinding[0];
        public HighlightBinding[] highlightBindings = new HighlightBinding[0];
        public EscapeInteractable HighlightedTarget => highlightedTarget;
        public int HighlightedRendererCount => highlightSnapshots.Count;
        public int GuardStatusLightCount => guardLights.Count;
        LineRenderer ring;
        MaterialPropertyBlock pulseBlock, highlightBlock, statusBlock;
        EscapeInteractable highlightedTarget;
        readonly List<RendererSnapshot> highlightSnapshots = new List<RendererSnapshot>();
        sealed class GuardLamp
        {
            public ExpandedEnemy guard;
            public Light light;
            public Action<ExpandedEnemyState,ExpandedEnemyState> changed;
        }
        readonly List<GuardLamp> guardLights=new List<GuardLamp>();
        sealed class RendererSnapshot
        {
            public Renderer renderer;
            public MaterialPropertyBlock original;
            public MaterialPropertyBlock[] materials;
        }
        float effectRemaining;
        bool subscribed;
        static readonly int BaseColor=Shader.PropertyToID("_BaseColor"),Emission=Shader.PropertyToID("_EmissionColor");

        void Start()
        {
            if(!session || !pulse){enabled=false;return;}
            pulseBlock=new MaterialPropertyBlock();highlightBlock=new MaterialPropertyBlock();
            var go=new GameObject("PulseFeedbackRing");go.transform.SetParent(transform,false);
            ring=go.AddComponent<LineRenderer>();ring.useWorldSpace=true;ring.loop=true;ring.positionCount=65;ring.widthMultiplier=artProfile ? .025f : .045f;
            ring.sharedMaterial=pulseMaterial;ring.enabled=false;BuildGuardLights();Subscribe();
            foreach(var lamp in guardLights)ApplyLight(lamp.light,lamp.guard.State);
        }
        void BuildGuardLights()
        {
            var used=new HashSet<Light>();
            foreach(var guard in session.Guards)
            {
                if(!guard)continue;Light status=null;
                if(guardStatusLights!=null)foreach(var binding in guardStatusLights)
                    if(binding!=null && binding.guard==guard && binding.statusLight){status=binding.statusLight;break;}
                if(!status && guard==session.enemy)status=enemyStatusLight;
                if(!status)foreach(var candidate in guard.GetComponentsInChildren<Light>(true))
                    if(candidate.name=="ChapterGuardStatus" || candidate.name=="GuardStatusLight"){status=candidate;break;}
                if(!status)continue;
                if(!used.Add(status)){Debug.LogError("CHAPTER_VISUALS: guards must have independent status lights");continue;}
                var lamp=new GuardLamp{guard=guard,light=status};lamp.changed=(previous,next)=>ApplyLight(lamp.light,next);guardLights.Add(lamp);
            }
        }
        void OnEnable(){if(ring)Subscribe();}
        void OnDisable(){Unsubscribe();ClearHighlight();if(ring)ring.enabled=false;}
        void OnDestroy(){Unsubscribe();ClearHighlight();}
        void Subscribe()
        {
            if(subscribed)return;
            foreach(var lamp in guardLights)if(lamp.guard)lamp.guard.StateChanged+=lamp.changed;
            pulse.Resolved+=OnPulse;session.RunEnded+=OnEnd;
            session.PauseChanged+=OnPause;session.interaction.InteractionEnded+=OnInteractionEnded;subscribed=true;
        }
        void Unsubscribe()
        {
            if(!subscribed)return;
            if(session)
            {
                foreach(var lamp in guardLights)if(lamp.guard)lamp.guard.StateChanged-=lamp.changed;
                session.RunEnded-=OnEnd;session.PauseChanged-=OnPause;
                if(session.interaction)session.interaction.InteractionEnded-=OnInteractionEnded;
            }
            if(pulse)pulse.Resolved-=OnPulse;subscribed=false;
        }
        void ApplyLight(Light status,ExpandedEnemyState state)
        {
            if(!status)return;
            status.color=state==ExpandedEnemyState.Alert?new Color(1,.68f,.18f):state==ExpandedEnemyState.Chase?new Color(1,.22f,.15f):state==ExpandedEnemyState.Disrupted?new Color(.28f,.9f,1):new Color(.55f,.8f,1);
            status.intensity=state==ExpandedEnemyState.Disrupted?.15f:state==ExpandedEnemyState.Chase?1.6f:state==ExpandedEnemyState.Alert?1.2f:.5f;
            var renderer=status.GetComponent<Renderer>();
            if(renderer)
            {
                if(statusBlock==null)statusBlock=new MaterialPropertyBlock();
                var tint=status.color*(state==ExpandedEnemyState.Disrupted?.25f:1f);tint.a=1;
                statusBlock.SetColor(BaseColor,tint);statusBlock.SetColor(Emission,tint);renderer.SetPropertyBlock(statusBlock);
            }
        }
        void OnPulse(PulseUseResult result)
        {
            if(result!=PulseUseResult.Success || !ring)return;
            effectRemaining=.45f;ring.transform.position=session.player.transform.position+Vector3.up*.12f;ring.enabled=true;
        }
        void OnEnd(RunSummary summary){effectRemaining=0;if(ring)ring.enabled=false;ClearHighlight();}
        void OnPause(bool paused){if(paused)ClearHighlight();}
        void OnInteractionEnded(EscapeInteractable target){if(target==highlightedTarget)ClearHighlight();}
        void Update()
        {
            if(!ring || !session || !session.Ready){ClearHighlight();return;}
            if(session.Outcome!=RunOutcome.Running){ClearHighlight();ring.enabled=false;return;}
            if(session.Paused){ClearHighlight();return;}
            if(effectRemaining>0)
            {
                effectRemaining=Mathf.Max(0,effectRemaining-Time.deltaTime);
                float progress=1-effectRemaining/.45f;
                float radius=Mathf.Lerp(.2f,pulse.profile.range,progress);
                Vector3 center=ring.transform.position;
                for(int i=0;i<ring.positionCount;i++){float angle=i*(Mathf.PI*2/(ring.positionCount-1));ring.SetPosition(i,center+new Vector3(Mathf.Sin(angle)*radius,0,Mathf.Cos(angle)*radius));}
                pulseBlock.SetColor(BaseColor,artProfile?new Color(.48f,.78f,.86f,.52f*(1-progress)):new Color(.3f,.95f,1,.7f*(1-progress)));ring.SetPropertyBlock(pulseBlock);
                ring.enabled=effectRemaining>0;
            }
            EscapeInteractable target=session.interaction.Current?session.interaction.Current:session.interaction.Candidate;
            if(target!=highlightedTarget)
            {
                ClearHighlight();
                highlightedTarget=target;
                BeginHighlight(target);
            }
        }
        void BeginHighlight(EscapeInteractable target)
        {
            if(!target)return;
            var selected=new HashSet<Renderer>();
            if(highlightBindings!=null)
                foreach(var binding in highlightBindings)
                    if(binding!=null && binding.target==target && binding.renderers!=null)
                        foreach(var renderer in binding.renderers)if(renderer)selected.Add(renderer);
            if(selected.Count==0 && target.anchor)
            {
                var marker=target.anchor.GetComponentInChildren<Renderer>();
                if(marker)selected.Add(marker);
            }
            foreach(var renderer in selected)
            {
                var snapshot=new RendererSnapshot { renderer=renderer, original=new MaterialPropertyBlock(),
                    materials=new MaterialPropertyBlock[renderer.sharedMaterials.Length] };
                renderer.GetPropertyBlock(snapshot.original);
                // Snapshot every indexed block before changing any block. A per-material
                // block overrides renderer-level values, so it needs the same highlight.
                for(int i=0;i<snapshot.materials.Length;i++)
                {
                    snapshot.materials[i]=new MaterialPropertyBlock();
                    renderer.GetPropertyBlock(snapshot.materials[i],i);
                }
                highlightSnapshots.Add(snapshot);
                if(!artProfile)
                {
                    renderer.GetPropertyBlock(highlightBlock);Tint(highlightBlock,renderer.sharedMaterial);renderer.SetPropertyBlock(highlightBlock);
                }
                for(int i=0;i<snapshot.materials.Length;i++)
                {
                    if(snapshot.materials[i].isEmpty && !artProfile)continue;
                    // An indexed art block keeps each material's own emission. A
                    // single renderer-level emission would overwrite lit screens.
                    // When there was no indexed override, inherit the original
                    // global block so its existing tint/texture values survive.
                    if(artProfile && snapshot.materials[i].isEmpty)renderer.GetPropertyBlock(highlightBlock);
                    else renderer.GetPropertyBlock(highlightBlock,i);
                    Tint(highlightBlock,renderer.sharedMaterials[i]);renderer.SetPropertyBlock(highlightBlock,i);
                }
            }
        }
        void Tint(MaterialPropertyBlock block,Material material)
        {
            if(artProfile)
            {
                // Preserve albedo, all texture channels and existing per-instance
                // tint. This is a small additive cue, not a solid mint silhouette.
                Color original=block.HasProperty(Emission)?block.GetColor(Emission):
                    material && material.HasProperty(Emission)?material.GetColor(Emission):Color.black;
                block.SetColor(Emission,original+artProfile.accentColor*Mathf.Clamp(artProfile.highlightStrength,0,.3f));
                return;
            }
            block.SetColor(BaseColor,new Color(.5f,1,.82f));
            block.SetColor(Emission,new Color(.45f,1,.75f)*1.4f);
        }
        void ClearHighlight()
        {
            foreach(var snapshot in highlightSnapshots)
            {
                if(!snapshot.renderer)continue;
                snapshot.renderer.SetPropertyBlock(snapshot.original.isEmpty?null:snapshot.original);
                int count=Mathf.Min(snapshot.materials.Length,snapshot.renderer.sharedMaterials.Length);
                for(int i=0;i<count;i++)
                    snapshot.renderer.SetPropertyBlock(snapshot.materials[i].isEmpty?null:snapshot.materials[i],i);
            }
            highlightSnapshots.Clear();highlightedTarget=null;
        }
    }
}
