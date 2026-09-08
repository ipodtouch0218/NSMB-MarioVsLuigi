using Quantum;
using UnityEngine;
using UnityEngine.Serialization;

public class TeamAsset : AssetObject, IOrderedAsset {

    int IOrderedAsset.Order => Order;

    public int Order;
    [FormerlySerializedAs("nameTranslationKey")] public string nameTranslationKey;
    public string textSpriteNormal, textSpriteColorblind, textSpriteColorblindBig;

#if QUANTUM_UNITY
    [ColorUsage(false)] public Color color;
    public Sprite spriteNormal, spriteColorblind;
#endif
}