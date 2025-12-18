using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public class CustomBlendshapeAdapter_Copresence : BlendshapeAdapter
{
    [Header("Target Settings")]
    [SerializeField] private List<SkinnedMeshRenderer> m_TargetMeshes = new List<SkinnedMeshRenderer>();
    [SerializeField] private float m_BlendShapeScale = 100f;

    private struct BlendShapeTarget 
    { 
        public SkinnedMeshRenderer mesh; 
        public int index; 
    }

    private Dictionary<string, string[]> nameMappingTable;
    private Dictionary<string, List<BlendShapeTarget>> globalBlendShapeMap;

    public override void Initialize()
    {
        InitializeMappingTable();

        globalBlendShapeMap = new Dictionary<string, List<BlendShapeTarget>>();
        
        foreach (var smr in m_TargetMeshes)
        {
            if (smr == null || smr.sharedMesh == null) continue;
            
            int count = smr.sharedMesh.blendShapeCount;
            for (int i = 0; i < count; i++)
            {
                string rawName = smr.sharedMesh.GetBlendShapeName(i);
                
                RegisterBlendShape(rawName, smr, i);

                foreach(var map in nameMappingTable)
                {
                    if(map.Value.Contains(rawName))
                    {
                        RegisterBlendShape(map.Key, smr, i);
                    }
                }
            }
        }
    }

    private void InitializeMappingTable()
    {
        nameMappingTable = new Dictionary<string, string[]>();

        nameMappingTable.Add("eyeBlinkLeft",     new[] { "EYES_CLOSED_L" });
        nameMappingTable.Add("eyeBlinkRight",    new[] { "EYES_CLOSED_R" });
        nameMappingTable.Add("eyeLookDownLeft",  new[] { "EYES_LOOK_DOWN_L" });
        nameMappingTable.Add("eyeLookDownRight", new[] { "EYES_LOOK_DOWN_R" });
        nameMappingTable.Add("eyeLookUpLeft",    new[] { "EYES_LOOK_UP_L" });
        nameMappingTable.Add("eyeLookUpRight",   new[] { "EYES_LOOK_UP_R" });
        
        nameMappingTable.Add("eyeLookInLeft",    new[] { "EYES_LOOK_RIGHT_L" });
        nameMappingTable.Add("eyeLookOutLeft",   new[] { "EYES_LOOK_LEFT_L" });
        nameMappingTable.Add("eyeLookInRight",   new[] { "EYES_LOOK_LEFT_R" });
        nameMappingTable.Add("eyeLookOutRight",  new[] { "EYES_LOOK_RIGHT_R" });

        nameMappingTable.Add("eyeWideLeft",      new[] { "UPPER_LID_RAISER_L" });
        nameMappingTable.Add("eyeWideRight",     new[] { "UPPER_LID_RAISER_R" });
        nameMappingTable.Add("eyeSquintLeft",    new[] { "LID_TIGHTENER_L" });
        nameMappingTable.Add("eyeSquintRight",   new[] { "LID_TIGHTENER_R" });

        nameMappingTable.Add("browDownLeft",     new[] { "BROW_LOWERER_L" });
        nameMappingTable.Add("browDownRight",    new[] { "BROW_LOWERER_R" });
        nameMappingTable.Add("browInnerUp",      new[] { "INNER_BROW_RAISER_L", "INNER_BROW_RAISER_R" }); 
        nameMappingTable.Add("browOuterUpLeft",  new[] { "OUTER_BROW_RAISER_L" });
        nameMappingTable.Add("browOuterUpRight", new[] { "OUTER_BROW_RAISER_R" });

        nameMappingTable.Add("cheekPuff",        new[] { "CHEEK_PUFF_L", "CHEEK_PUFF_R" });
        nameMappingTable.Add("cheekSquintLeft",  new[] { "CHEEK_RAISER_L" });
        nameMappingTable.Add("cheekSquintRight", new[] { "CHEEK_RAISER_R" });
        
        nameMappingTable.Add("noseSneerLeft",    new[] { "NOSE_WRINKLER_L" });
        nameMappingTable.Add("noseSneerRight",   new[] { "NOSE_WRINKLER_R" });

        nameMappingTable.Add("jawOpen",          new[] { "JAW_DROP" });
        nameMappingTable.Add("jawForward",       new[] { "JAW_THRUST" });
        nameMappingTable.Add("jawLeft",          new[] { "JAW_SIDEWAYS_LEFT" });
        nameMappingTable.Add("jawRight",         new[] { "JAW_SIDEWAYS_RIGHT" });

        nameMappingTable.Add("mouthClose",       new[] { "LIPS_TOWARD" }); 
        nameMappingTable.Add("mouthLeft",        new[] { "MOUTH_LEFT" });
        nameMappingTable.Add("mouthRight",       new[] { "MOUTH_RIGHT" });
        
        nameMappingTable.Add("mouthSmileLeft",   new[] { "LIP_CORNER_PULLER_L" });
        nameMappingTable.Add("mouthSmileRight",  new[] { "LIP_CORNER_PULLER_R" });
        nameMappingTable.Add("mouthFrownLeft",   new[] { "LIP_CORNER_DEPRESSOR_L" });
        nameMappingTable.Add("mouthFrownRight",  new[] { "LIP_CORNER_DEPRESSOR_R" });
        
        nameMappingTable.Add("mouthDimpleLeft",  new[] { "DIMPLER_L" });
        nameMappingTable.Add("mouthDimpleRight", new[] { "DIMPLER_R" });
        nameMappingTable.Add("mouthStretchLeft", new[] { "LIP_STRETCHER_L" });
        nameMappingTable.Add("mouthStretchRight", new[] { "LIP_STRETCHER_R" });
        
        nameMappingTable.Add("mouthPressLeft",   new[] { "LIP_PRESSOR_L" });
        nameMappingTable.Add("mouthPressRight",  new[] { "LIP_PRESSOR_R" });
        
        nameMappingTable.Add("mouthLowerDownLeft",  new[] { "LOWER_LIP_DEPRESSOR_L" });
        nameMappingTable.Add("mouthLowerDownRight", new[] { "LOWER_LIP_DEPRESSOR_R" });
        nameMappingTable.Add("mouthUpperUpLeft",    new[] { "UPPER_LIP_RAISER_L" });
        nameMappingTable.Add("mouthUpperUpRight",   new[] { "UPPER_LIP_RAISER_R" });

        nameMappingTable.Add("mouthFunnel",      new[] { "LIP_FUNNELER_LB", "LIP_FUNNELER_LT", "LIP_FUNNELER_RB", "LIP_FUNNELER_RT" });
        nameMappingTable.Add("mouthPucker",      new[] { "LIP_PUCKER_L", "LIP_PUCKER_R" });
        
        nameMappingTable.Add("mouthRollLower",   new[] { "LIP_SUCK_LB", "LIP_SUCK_RB" });
        nameMappingTable.Add("mouthRollUpper",   new[] { "LIP_SUCK_LT", "LIP_SUCK_RT" });
        
        nameMappingTable.Add("mouthShrugLower",  new[] { "CHIN_RAISER_B", "CHIN_RAISER_T" });
        nameMappingTable.Add("mouthShrugUpper",  new[] { "UPPER_LID_RAISER_L", "UPPER_LID_RAISER_R" });

        nameMappingTable.Add("tongueOut",        new[] { "TONGUE_OUT" });
    }

    private void RegisterBlendShape(string keyName, SkinnedMeshRenderer smr, int index)
    {
        if (!globalBlendShapeMap.ContainsKey(keyName)) 
            globalBlendShapeMap[keyName] = new List<BlendShapeTarget>();
        
        var list = globalBlendShapeMap[keyName];
        bool alreadyExists = false;
        for(int i=0; i<list.Count; i++) {
            if(list[i].mesh == smr && list[i].index == index) {
                alreadyExists = true;
                break;
            }
        }

        if(!alreadyExists)
            list.Add(new BlendShapeTarget { mesh = smr, index = index });
    }

    public override void SetBlendshapeWeight(string shapeName, float weight)
    {
        if (globalBlendShapeMap == null) return;

        if (globalBlendShapeMap.TryGetValue(shapeName, out var targets))
        {
            float scaledValue = weight * m_BlendShapeScale;
            foreach (var target in targets) 
            {
                if(target.mesh != null)
                    target.mesh.SetBlendShapeWeight(target.index, scaledValue);
            }
        }
    }

    public override void ResetAll()
    {
        foreach (var smr in m_TargetMeshes)
        {
            if (smr == null) continue;
            int count = smr.sharedMesh.blendShapeCount;
            for (int i = 0; i < count; i++) smr.SetBlendShapeWeight(i, 0);
        }
    }
}