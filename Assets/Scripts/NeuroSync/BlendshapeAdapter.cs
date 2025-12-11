using UnityEngine;

public abstract class BlendshapeAdapter : MonoBehaviour
{
    public abstract void Initialize();
    public abstract void SetBlendshapeWeight(string shapeName, float weight);
    public abstract void ResetAll();
}