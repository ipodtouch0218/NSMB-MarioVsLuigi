using NSMB.Extensions;
using Photon.Deterministic;
using Quantum;
using UnityEngine;
using UnityEngine.Tilemaps;

public class TilemapAnimator : MonoBehaviour {

    //---Serialized Variables
    [SerializeField] private Tilemap tilemap;
    [SerializeField] private ParticleSystem tileBreakParticleSystem;

    //---Private Variables
    private VersusStageData stage;


    public void OnValidate() {
        this.SetIfNull(ref tilemap);
    }

    public void Start() {
        QuantumEvent.Subscribe<EventTileChanged>(this, OnTileChanged);
        QuantumEvent.Subscribe<EventTileBroken>(this, OnTileBroken);
        stage = (VersusStageData) QuantumUnityDB.GetGlobalAsset(FindObjectOfType<QuantumMapData>().Asset.UserAsset);
    }

    private void OnTileBroken(EventTileBroken e) {
        ParticleSystem particle = Instantiate(tileBreakParticleSystem,
            QuantumUtils.RelativeTileToWorld(stage, new FPVector2(e.TileX, e.TileY)).ToUnityVector2() + (Vector2.one * 0.25f), Quaternion.identity);

        if (QuantumUnityDB.GetGlobalAsset(e.Tile.Tile) is BreakableBrickTile bbt) {
            var main = particle.main;
            main.startColor = bbt.ParticleColor;
        }

        if (particle.TryGetComponent(out AudioSource sfx)) {
            sfx.PlayOneShot(
                e.Frame.TryGet(e.Entity, out MarioPlayer mario) && mario.CurrentPowerupState == PowerupState.MegaMushroom
                    ? SoundEffect.Powerup_MegaMushroom_Break_Block
                    : SoundEffect.World_Block_Break);
        }

        particle.Play();
    }

    private void OnTileChanged(EventTileChanged e) {
        var tile = QuantumUnityDB.GetGlobalAsset(e.NewTile.Tile);
        TileBase unityTile = tile ? tile.Tile : null;
        Vector3Int coords = new(e.TileX, e.TileY, 0);

        tilemap.SetTile(coords, unityTile);
        Matrix4x4 mat = Matrix4x4.TRS(default, Quaternion.Euler(0, 0, e.NewTile.Rotation.AsFloat), new Vector3(e.NewTile.Scale.X.AsFloat, e.NewTile.Scale.Y.AsFloat, 1));
        tilemap.SetTransformMatrix(coords, mat);

        tilemap.RefreshTile(coords);
    }
}
