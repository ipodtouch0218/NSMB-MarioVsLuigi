using Quantum;

public unsafe class RouletteTile : PowerupTileBase {
    public override unsafe CoinItemAsset GetItemAsset(Frame f, EntityRef marioEntity, MarioPlayer* mario) {
        var gamemode = f.FindAsset(f.Global->Rules.Gamemode);
        return gamemode.GetRandomItem(f, mario, true);
    }
}