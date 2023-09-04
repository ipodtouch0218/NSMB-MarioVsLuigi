using UnityEngine;

using NSMB.Tiles;

namespace NSMB.Entities {
    public interface IBlockBumpable {

        void BlockBump(BasicEntity bumper, Vector2Int tile, InteractionDirection direction);

    }
}
