using Quantum;
using UnityEngine;

public class CharacterAsset : AssetObject {

    public AssetRef<EntityPrototype> Prototype;

    public string SoundFolder;
    public string UiString;
    public string TranslationString;

    public Sprite LoadingSmallSprite;
    public Sprite LoadingLargeSprite;
    public Sprite ReadySprite;

    public RuntimeAnimatorController SmallOverrides;
    public RuntimeAnimatorController LargeOverrides;

}