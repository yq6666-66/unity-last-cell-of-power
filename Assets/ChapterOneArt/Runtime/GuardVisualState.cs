using UnityEngine;
using LastCellOfPower.WarehouseExpanded;

namespace LastCellOfPower.ChapterOneArt
{
    /// <summary>Read-only scanner display. Never changes enemy sensing or state.</summary>
    public sealed class GuardVisualState : MonoBehaviour
    {
        public ExpandedEnemy enemy;
        public Renderer[] scannerRenderers=new Renderer[0];
        MaterialPropertyBlock block;
        MaterialPropertyBlock[] original;
        ExpandedEnemyState previous;
        bool applied;
        static readonly int Emission=Shader.PropertyToID("_EmissionColor"),BaseColor=Shader.PropertyToID("_BaseColor");
        void Start()
        {
            block=new MaterialPropertyBlock();original=new MaterialPropertyBlock[scannerRenderers.Length];
            for(int i=0;i<scannerRenderers.Length;i++)
            {
                original[i]=new MaterialPropertyBlock();
                if(scannerRenderers[i])scannerRenderers[i].GetPropertyBlock(original[i]);
            }
            Refresh();
        }
        void Update(){Refresh();}
        public void Refresh()
        {
            if(!enemy || block==null || (applied && previous==enemy.State))return;
            previous=enemy.State;applied=true;
            Color color=previous==ExpandedEnemyState.Alert?new Color(1,.53f,.12f):
                previous==ExpandedEnemyState.Chase?new Color(1,.13f,.065f):
                previous==ExpandedEnemyState.Disrupted?new Color(.08f,.24f,.29f):new Color(.28f,.52f,.61f);
            float strength=previous==ExpandedEnemyState.Disrupted ? .5f : previous==ExpandedEnemyState.Chase ? 1.8f : 1.25f;
            foreach(var renderer in scannerRenderers)
            {
                if(!renderer)continue;renderer.GetPropertyBlock(block);
                block.SetColor(BaseColor,color*.7f);block.SetColor(Emission,color*strength);renderer.SetPropertyBlock(block);
            }
        }
        void OnDisable()
        {
            if(original==null)return;
            for(int i=0;i<Mathf.Min(original.Length,scannerRenderers.Length);i++)
                if(scannerRenderers[i])scannerRenderers[i].SetPropertyBlock(original[i].isEmpty?null:original[i]);
            applied=false;
        }
    }
}
