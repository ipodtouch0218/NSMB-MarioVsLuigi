using UnityEngine;

public interface IBlockBumpable {

    void BlockBump(BasicEntity bumper, Vector3Int tile, InteractableTile.InteractionDirection direction);

}
