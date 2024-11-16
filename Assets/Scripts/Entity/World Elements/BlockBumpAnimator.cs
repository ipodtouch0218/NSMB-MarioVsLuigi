using NSMB.Extensions;
using NSMB.Tiles;
using Quantum;
using UnityEngine;
using UnityEngine.Tilemaps;

public unsafe class BlockBumpAnimator : QuantumEntityViewComponent {

    //---Serialized Variables
    [SerializeField] private SpriteRenderer sRenderer;
    [SerializeField] private AudioSource sfx;

    public void OnValidate() {
        this.SetIfNull(ref sRenderer);
        this.SetIfNull(ref sfx);
    }

    public override void OnActivate(Frame f) {
        var blockBump = f.Unsafe.GetPointer<BlockBump>(EntityRef);

        StageTile stageTile = QuantumUnityDB.GetGlobalAsset(blockBump->StartTile);
        TileBase tile = stageTile.Tile;
        Sprite sprite;
        if (tile is SiblingRuleTile tp) {
            sprite = tp.m_DefaultSprite;
        } else if (tile is AnimatedTile at) {
            sprite = at.m_AnimatedSprites[0];
        } else if (tile is Tile t) {
            sprite = t.sprite;
        } else {
            sprite = null;
        }
        sRenderer.sprite = sprite;

        if (!NetworkHandler.IsReplayFastForwarding) {
            sfx.Play();
        }
    }

    public override void OnUpdateView() {
        Frame f = PredictedFrame;
        if (!f.Unsafe.TryGetPointer(EntityRef, out BlockBump* blockBump)) {
            return;
        }
    
        float bumpScale = 0.35f;
        float bumpDuration = 0.25f;

        float remainingTime = blockBump->Lifetime / 60f;
        float size = Mathf.Sin((remainingTime / bumpDuration) * Mathf.PI) * bumpScale * 0.5f;

        transform.localScale = new(0.5f + size, 0.5f + size, 1);
        transform.position = blockBump->Origin.ToUnityVector2() + new Vector2(0, size * (blockBump->IsDownwards ? -1 : 1));
    }
}