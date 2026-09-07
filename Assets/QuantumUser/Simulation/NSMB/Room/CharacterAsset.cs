using Photon.Deterministic;
using Quantum;
using System;
using System.Collections.Generic;
using UnityEngine;

public class CharacterAsset : AssetObject, ISoundOverrideProvider, IOrderedAsset {

    int IOrderedAsset.Order => Order;

    public AssetRef<EntityPrototype> Prototype;

    public string UiString;
    public string TranslationString;

#if QUANTUM_UNITY
    public Sprite LoadingSmallSprite;
    public Sprite LoadingLargeSprite;
    public Sprite ReadySprite;
    public Sprite SilhouetteSprite;

    public Sprite SelectionSprite;
    public Color SelectionColor = Color.white;
    public Color[] lightBarColors = new Color[10];
#endif

    public int Order;

    public SoundEffectOverride[] SfxOverrides;

    [NonSerialized] private Dictionary<SoundEffect, SoundEffectOverride> overridesDict;

    public override void Loaded(IResourceManager resourceManager, Native.Allocator allocator) {
        overridesDict = new();
        if (SfxOverrides != null) {
            foreach (var @override in SfxOverrides) {
                overridesDict[@override.SoundEffect] = @override;
            }
        }
    }

    public SoundEffectOverride GetOverride(SoundEffect sfx) {
        overridesDict.TryGetValue(sfx, out var result);
        return result;
    }
}