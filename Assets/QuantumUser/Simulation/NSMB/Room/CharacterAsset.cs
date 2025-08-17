using Quantum;
using UnityEngine;
using System.Linq;

public class CharacterAsset : AssetObject {

    public AssetRef<EntityPrototype> Prototype;

    public string SoundFolder;
    public string UiString;
    public string TranslationString;

#if QUANTUM_UNITY
    public Sprite LoadingSmallSprite;
    public Sprite LoadingLargeSprite;
    public Sprite ReadySprite;

    public RuntimeAnimatorController SmallOverrides;
    public RuntimeAnimatorController LargeOverrides;
#endif 

//#if QUANTUM_UNITY
    public StageThemeOverride[] SpriteList;
    public CharacterStateAnimation DefaultSprite;
    public CharacterStateAnimation GetAnimData(StageTheme gameStyle, CharacterState animState, PowerupState itemState) {
        var ReturnSprite = SpriteList.FirstOrDefault(sto => sto.Theme == gameStyle)?.States.FirstOrDefault(csa => csa.State == animState && csa.Item == itemState) ?? DefaultSprite;
        return ReturnSprite;
    }
//#endif
}

public enum StageTheme {
    NSMB,
    SMBDX,
    //More later
}

public enum CharacterState {
    IDLE, //
    WALK, //
    RUN, //
    FASTRUN, //
    MACH4, //
    PUSH, //
    SKID, //
    JUMPRISE, //
    JUMPFALL, //
    FALL, //
    DOUBLEJUMP, //
    TRIPLEJUMP, //
    WALLSLIDE, //
    WALLJUMPRISE, //
    WALLJUMPFALL, //
    JUMPLAND, //
    CROUCH, //
    GROUNDPOUNDSTART, //
    GROUNDPOUND, //
    WEAKKNOCKBACK, //
    WEAKKNOCKFORWARD, //
    STRONGKNOCKBACK, //
    STRONGKNOCKFORWARD, //
    WATERKNOCKBACK, //
    DIE,//
    SHOOT, //
    SHELLSPIN, //
    MEGAGROW, //
    MEGACANCEL, //
    SPIN, //
    SPINFLY, //
    PROPELLERFLY, //
    PROPELLERTWIRL, //
    DRILL, //
    HOLDIDLE, //
    HOLDWALK, //
    HOLDRUN, //
    HOLDFASTRUN, //
    HOLDMACH4, //
    HOLDJUMPRISE, //
    HOLDJUMPFALL, //
    HOLDFALL, //
    HOLDUPIDLE, //
    HOLDUPWALK, //
    HOLDUPRUN, //
    HOLDUPFASTRUN, //
    HOLDUPMACH4, //
    HOLDUPJUMPRISE, //
    HOLDUPJUMPFALL, //
    HOLDUPFALL, //
    CARRYSTART, //
    THROW, //
    SWIM, //
    SWIMPADDLE, //
    SWIMKICK, //
    HOLDSWIM, //
    DIEWATER, //
    PIPEENTER, //
    SLIDE, 
}

[System.Serializable]
public class StageThemeOverride {
    public StageTheme Theme;
    public CharacterStateAnimation[] States;
}

[System.Serializable]
public class CharacterStateAnimation {
    public CharacterState State;
    public PowerupState Item;
    public Sprite[] Sprites;
    public float Fps;
}
[System.Serializable]
public class InvincibilityColors {
    public Gradient HatGradient;
    public Gradient OverallsGradient;
    public Gradient SkinGradient;
}