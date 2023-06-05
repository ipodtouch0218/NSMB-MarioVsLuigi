using UnityEngine;

using Fusion;
using NSMB.Entities;
using NSMB.Entities.Enemies;
using NSMB.Entities.Player;
using NSMB.Game;

namespace NSMB.Tiles {

    [CreateAssetMenu(fileName = "BreakableBrickTile", menuName = "ScriptableObjects/Tiles/BreakableBrickTile")]
    public class BreakableBrickTile : InteractableTile {

        //---Static Variables
        private static readonly Vector2 SpawnOffset = new(0, 0.25f);

        //---Serialized Variables
        [SerializeField] protected Color particleColor;
        [SerializeField] public bool breakableBySmallMario = false, breakableByLargeMario = true, breakableByGiantMario = true, breakableByShells = true, breakableByBombs = true, bumpIfNotBroken = true, bumpIfBroken = true;
        [SerializeField] private Vector2Int tileSize = Vector2Int.one;

        protected bool BreakBlockCheck(BasicEntity interacter, InteractionDirection direction, Vector3 worldLocation) {
            bool doBump = false, doBreak = false, giantBreak = false;
            if (interacter is PlayerController pl) {
                if (pl.State <= Enums.PowerupState.MiniMushroom && !pl.IsDrilling) {
                    doBreak = breakableBySmallMario;
                    doBump = true;
                } else if (pl.State == Enums.PowerupState.MegaMushroom) {
                    doBreak = breakableByGiantMario;
                    giantBreak = true;
                    doBump = false;
                } else if (pl.State >= Enums.PowerupState.Mushroom || pl.IsDrilling) {
                    doBreak = breakableByLargeMario;
                    doBump = true;
                }
            } else if (interacter is KoopaWalk) {
                doBump = true;
                doBreak = breakableByShells;
            } else if (interacter is BobombWalk) {
                doBump = false;
                doBreak = breakableByBombs;
            }

            if (doBump && doBreak && bumpIfBroken)
                Bump(interacter, direction, worldLocation);
            if (doBump && !doBreak && bumpIfNotBroken)
                BumpWithAnimation(interacter, direction, worldLocation);
            if (doBreak)
                Break(interacter, worldLocation, giantBreak ? Enums.Sounds.Powerup_MegaMushroom_Break_Block : Enums.Sounds.World_Block_Break);

            return doBreak;
        }

        public void Break(BasicEntity interacter, Vector3 worldLocation, Enums.Sounds sound) {
            Vector2Int tileLocation = Utils.Utils.WorldToTilemapPosition(worldLocation);

            // Tilemap
            GameManager.Instance.tileManager.SetTile(tileLocation, null);

            if (interacter && interacter.Runner.IsForward) {
                // Particle
                for (int x = 0; x < tileSize.x; x++) {
                    for (int y = 0; y < tileSize.y; y++) {
                        Vector2Int offset = new(x, y);
                        GameManager.Instance.particleManager.Play(Enums.Particle.Entity_BrickBreak, Utils.Utils.TilemapToWorldPosition(tileLocation + offset) + Vector3.one * 0.25f, particleColor);
                    }
                }

                // Sound
                if (interacter)
                    interacter.PlaySound(sound);
            }
        }

        public void BumpWithAnimation(BasicEntity interacter, InteractionDirection direction, Vector3 worldLocation) {
            Bump(interacter, direction, worldLocation);
            Vector2Int tileLocation = Utils.Utils.WorldToTilemapPosition(worldLocation);

            // Bump
            bool downwards = direction == InteractionDirection.Down;
            GameData.Instance.BumpBlock((short) tileLocation.x, (short) tileLocation.y, this,
                this, downwards, downwards ? -SpawnOffset : SpawnOffset, false, NetworkPrefabRef.Empty);
        }

        public override bool Interact(BasicEntity interacter, InteractionDirection direction, Vector3 worldLocation, out bool bumpSound) {
            // Breaking block check.
            bool broken = BreakBlockCheck(interacter, direction, worldLocation);

            bumpSound = !broken;
            if (interacter is PlayerController player) {
                bumpSound &= !player.IsGroundpounding;
                bumpSound &= !player.IsDrilling;
            }

            return broken;
        }
    }
}
