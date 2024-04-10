using UnityEngine;
using UnityEngine.Tilemaps;

using Fusion;
using NSMB.Entities.Player;
using NSMB.Entities.Collectable;
using NSMB.Game;
using NSMB.Tiles;

namespace NSMB.Entities.World {

    public class RespawningInvisibleBlock : NetworkBehaviour, IPlayerInteractable, IHaveTileDependencies {

        //---Static Variables
        private static readonly Vector3 BlockOffset = new(0.25f, 0.25f);
        private static readonly Color GizmoColor = new(1, 1, 1, 0.5f);
        private static readonly Vector2 SpawnOffset = new(0, -0.25f);

        //---Networked Variables
        [Networked] private TickTimer BumpTimer { get; set; }

        //---Serialized Variables
        [SerializeField] private TileBase bumpTile;
        [SerializeField] private TileBase resultTile;

        public void OnValidate() {
            Start();
        }

        public void Start() {
            transform.position = new Vector3(Mathf.FloorToInt(transform.position.x * 2) / 2f, Mathf.FloorToInt(transform.position.y * 2) / 2f, transform.position.z) + BlockOffset;
        }

        public void InteractWithPlayer(PlayerController player, PhysicsDataStruct.IContactStruct contact = null) {
            if (!BumpTimer.ExpiredOrNotRunning(Runner)) {
                return;
            }

            // No block can be at our location
            Vector2Int tileLocation = Utils.Utils.WorldToTilemapPosition(transform.position);
            if (Utils.Utils.GetTileAtTileLocation(tileLocation)) {
                return;
            }

            // Player has to be moving upwards
            if (player.body.Velocity.y < 0.1f) {
                return;
            }

            // Player has to bump us from below
            if (player.body.Position.y + (player.MainHitbox.size.y * player.body.transform.lossyScale.y) - (player.body.Velocity.y * Runner.DeltaTime) > transform.position.y) {
                return;
            }

            BumpTimer = TickTimer.CreateFromSeconds(Runner, 0.25f);
            DoBump(tileLocation, player);
            player.BlockBumpSoundCounter++;

            // Stop player velocity
            player.body.Velocity = new(player.body.Velocity.x, 0);
        }

        public void DoBump(Vector2Int tileLocation, PlayerController player) {
            Vector3 location = Utils.Utils.TilemapToWorldPosition(tileLocation) + BlockOffset;
            Coin.GivePlayerCoin(player, location);

            GameManager.Instance.BumpBlock((short) tileLocation.x, (short) tileLocation.y, bumpTile,
                resultTile, false, SpawnOffset, true, NetworkPrefabRef.Empty);
        }

        public TileBase[] GetTileDependencies() {
            return new TileBase[] { bumpTile, resultTile };
        }

#if UNITY_EDITOR
        public void OnDrawGizmos() {
            Gizmos.DrawIcon(new Vector3(Mathf.FloorToInt(transform.position.x * 2) / 2f, Mathf.FloorToInt(transform.position.y * 2) / 2f, transform.position.z) + BlockOffset, "HiddenBlock", true, GizmoColor);
        }
#endif
    }
}
