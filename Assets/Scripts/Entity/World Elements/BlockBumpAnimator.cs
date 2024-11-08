using NSMB.Extensions;
using NSMB.Tiles;
using Quantum;
using UnityEngine;
using UnityEngine.Tilemaps;

public unsafe class BlockBumpAnimator : QuantumCallbacks {

    //---Serialized Variables
    [SerializeField] private QuantumEntityView entity;
    [SerializeField] private SpriteRenderer sRenderer;
    [SerializeField] private AudioSource sfx;

    public void OnValidate() {
        this.SetIfNull(ref entity);
        this.SetIfNull(ref sRenderer);
        this.SetIfNull(ref sfx);
    }

    public void Initialize(QuantumGame game) {
        var blockBump = game.Frames.Predicted.Unsafe.GetPointer<BlockBump>(entity.EntityRef);

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

    public override void OnUpdateView(QuantumGame game) {
        Frame f = game.Frames.Predicted;
        if (!f.Unsafe.TryGetPointer(entity.EntityRef, out BlockBump* blockBump)) {
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