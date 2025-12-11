using UnityEngine;
using System.Collections.Generic;

public class StandardBlendshapeAdapter : BlendshapeAdapter
{
    [Header("Target Settings")]
    [SerializeField] private List<SkinnedMeshRenderer> m_TargetMeshes = new List<SkinnedMeshRenderer>();
    [SerializeField] private float m_BlendShapeScale = 100f;

    private struct BlendShapeTarget 
    { 
        public SkinnedMeshRenderer mesh; 
        public int index; 
    }

    private Dictionary<string, List<BlendShapeTarget>> globalBlendShapeMap;

    private Dictionary<string, string> nameMappingTable;

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
                    if(map.Value == rawName)
                    {
                        RegisterBlendShape(map.Key, smr, i);
                    }
                }
            }
        }
    }

    private void InitializeMappingTable()
    {
        nameMappingTable = new Dictionary<string, string>();

        nameMappingTable.Add("eyeBlinkLeft", "eyeBlinking_Left");
        nameMappingTable.Add("eyeBlinkRight", "eyeBlinking_Right");
        nameMappingTable.Add("browDownLeft", "browDown_Left");
        nameMappingTable.Add("browDownRight", "browDown_Right");
        nameMappingTable.Add("eyeWideLeft", "eyeWide_Left");
        nameMappingTable.Add("eyeWideRight", "eyeWide_Right");

        nameMappingTable.Add("eyeSquintLeft", "eyeSquint_L");
        nameMappingTable.Add("eyeSquintRight", "eyeSquint_R");
        nameMappingTable.Add("eyeLookInLeft", "eyeLookIn_L");
        nameMappingTable.Add("eyeLookInRight", "eyeLookIn_R");
        nameMappingTable.Add("eyeLookOutLeft", "eyeLookOut_L");
        nameMappingTable.Add("eyeLookOutRight", "eyeLookOut_R");
        nameMappingTable.Add("eyeLookUpLeft", "eyeLookUp_L");
        nameMappingTable.Add("eyeLookUpRight", "eyeLookUp_R");
        nameMappingTable.Add("eyeLookDownLeft", "eyeLookDown_L");
        nameMappingTable.Add("eyeLookDownRight", "eyeLookDown_R");
        
        nameMappingTable.Add("browOuterUpLeft", "browOuterUp_L");
        nameMappingTable.Add("browOuterUpRight", "browOuterUp_R");
        
        nameMappingTable.Add("cheekSquintLeft", "cheekSquint_L");
        nameMappingTable.Add("cheekSquintRight", "cheekSquint_R");
        nameMappingTable.Add("noseSneerLeft", "noseSneer_L");
        nameMappingTable.Add("noseSneerRight", "noseSneer_R");
        
        nameMappingTable.Add("mouthSmileLeft", "mouthSmile_L");
        nameMappingTable.Add("mouthSmileRight", "mouthSmile_R");
        nameMappingTable.Add("mouthFrownLeft", "mouthFrown_L");
        nameMappingTable.Add("mouthFrownRight", "mouthFrown_R");
        nameMappingTable.Add("mouthDimpleLeft", "mouthDimple_L");
        nameMappingTable.Add("mouthDimpleRight", "mouthDimple_R");
        nameMappingTable.Add("mouthStretchLeft", "mouthStretch_L");
        nameMappingTable.Add("mouthStretchRight", "mouthStretch_R");
        nameMappingTable.Add("mouthPressLeft", "mouthPress_L");
        nameMappingTable.Add("mouthPressRight", "mouthPress_R");
        nameMappingTable.Add("mouthLowerDownLeft", "mouthLowerDown_L");
        nameMappingTable.Add("mouthLowerDownRight", "mouthLowerDown_R");
        nameMappingTable.Add("mouthUpperUpLeft", "mouthUpperUp_L");
        nameMappingTable.Add("mouthUpperUpRight", "mouthUpperUp_R");

        nameMappingTable.Add("tongueOut", "tongue_tongueOut");
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