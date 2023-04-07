using UnityEngine;

using NSMB.Tiles;

public interface IBlockBumpable {

    void BlockBump(BasicEntity bumper, Vector2Int tile, InteractableTile.InteractionDirection direction);

}
