using UnityEngine;

using NSMB.Utils;

public abstract class Coin : CollectableEntity {

    public override void InteractWithPlayer(PlayerController player) {
        if (IsCollected)
            return;

        IsCollected = true;
        GivePlayerCoin(player);
    }

    public void GivePlayerCoin(PlayerController player) {
        byte newCoins = (byte) (player.Coins + 1);
        if (newCoins >= GameManager.Instance.coinRequirement) {
            player.SpawnRandomItem();
            newCoins = 0;
        }
        player.Coins = newCoins;

        Instantiate(Resources.Load("Prefabs/Particle/CoinCollect"), transform.position, Quaternion.identity);
        player.PlaySound(Enums.Sounds.World_Coin_Collect);

        NumberParticle num = Instantiate(PrefabList.Particle_CoinCollect, transform.position, Quaternion.identity);
        num.ApplyColorAndText(Utils.GetSymbolString(player.Coins.ToString(), Utils.numberSymbols), player.animationController.GlowColor);
    }
}
