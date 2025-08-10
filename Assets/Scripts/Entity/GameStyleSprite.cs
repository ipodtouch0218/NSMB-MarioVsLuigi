using NSMB.Utilities.Components;
using UnityEngine;
using NSMB.Utilities;

public class GameStyleSpriteOutput : MonoBehaviour {
    public LegacyAnimateSpriteRenderer SpriteAnimator;
    public SpriteRenderer SpriteSingleAnimator;
    public CharacterStateAnimation[] SpriteList;
    public bool UseSingleRenderer;

    void Start() {
        Update();
    }
    void Update() {
        if (UseSingleRenderer) {
            SpriteSingleAnimator.sprite = SpriteList[(int) Utils.GetStageTheme()].Sprites[0];
        }
        if (SpriteAnimator != null) {
            SpriteAnimator.fps = SpriteList[(int) Utils.GetStageTheme()].Fps;
            SpriteAnimator.frames = SpriteList[(int) Utils.GetStageTheme()].Sprites;
        }
    }
}
