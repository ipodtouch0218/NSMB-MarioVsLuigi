using UnityEngine.Tilemaps;

namespace NSMB.Tiles {
    public interface IHaveTileDependencies {

        TileBase[] GetTileDependencies();

    }
}
