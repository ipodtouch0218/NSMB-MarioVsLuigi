using UnityEngine;

public interface IBlockBumpable {

    void BlockBump(BasicEntity bumper, Vector2Int tile, InteractableTile.InteractionDirection direction);

}
