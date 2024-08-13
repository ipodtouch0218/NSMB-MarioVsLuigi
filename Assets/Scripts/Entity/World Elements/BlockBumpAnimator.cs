using NSMB.Extensions;
using NSMB.Tiles;
using UnityEngine;
using UnityEngine.Tilemaps;
using Quantum;

public class BlockBumpAnimator : QuantumCallbacks {

    //---Serialized Variables
    [SerializeField] private QuantumEntityView entity;
    [SerializeField] private SpriteRenderer sRenderer;

    public void OnValidate() {
        this.SetIfNull(ref entity);
        this.SetIfNull(ref sRenderer);
    }

    public void Initialize(QuantumGame game) {
        var blockBump = game.Frames.Predicted.Get<BlockBump>(entity.EntityRef);

        /*
        // Spawn coin effect immediately
        if (SpawnCoin) {
            GameObject coin = Instantiate(PrefabList.Instance.Particle_CoinFromBlock, transform.position + new Vector3(0, IsDownwards ? -0.25f : 0.5f), Quaternion.identity);
            coin.GetComponentInChildren<Animator>().SetBool("down", IsDownwards);
        }
        */

        StageTile stageTile = QuantumUnityDB.GetGlobalAsset(blockBump.StartTile);
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
    }

    public override void OnUpdateView(QuantumGame game) {
        Frame f = game.Frames.Predicted;
        if (!f.Exists(entity.EntityRef)) {
            return;
        }
    
        float bumpScale = 0.35f;
        float bumpDuration = 0.25f;

        var blockBump = game.Frames.Predicted.Get<BlockBump>(entity.EntityRef);

        float remainingTime = blockBump.Lifetime / 60f;
        float size = Mathf.Sin((remainingTime / bumpDuration) * Mathf.PI) * bumpScale * 0.5f;

        transform.localScale = new(0.5f + size, 0.5f + size, 1);
        transform.position = blockBump.Origin.ToUnityVector2() + new Vector2(0, size * (blockBump.IsDownwards ? -1 : 1));
    }
}