using UnityEngine;
using UniVRM10;

public class VRMExpressionTest : MonoBehaviour
{
    public Vrm10Instance vrm;

    void Start()
    {
        var key = ExpressionKey.CreateFromPreset(ExpressionPreset.angry);
        vrm.Runtime.Expression.SetWeight(key, 1.0f);
    }
}