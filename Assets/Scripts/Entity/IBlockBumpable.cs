using UnityEngine;

public interface IBlockBumpable {

    void Bump(BasicEntity bumper, Vector3Int tile, InteractableTile.InteractionDirection direction);

}
