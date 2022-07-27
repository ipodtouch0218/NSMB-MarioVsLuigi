using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using Photon.Pun;

[CreateAssetMenu(fileName = "x10CoinsTile", menuName = "ScriptableObjects/Tiles/x10CoinsTile", order = 1)]
public class x10_CoinsTile : BreakableBrickTile
{ 
    public string resultTile;
    private bool x10 = true;
    private protected int remainingCoins = 10;
    private protected int remainingTime = 3;
    private protected bool First = true;

    void Start()
    {
        if (First)
        {
            remainingCoins = 10;
            Timer();
            First = false;
        }

        remainingCoins = remainingCoins - 1;
        Debug.Log("Coins:" + remainingCoins);
    }

    public override bool Interact(MonoBehaviour interacter, InteractionDirection direction, Vector3 worldLocation)
    {
        Debug.Log("Holaw");
        //if (base.Interact(interacter, direction, worldLocation))
          //  return true;

        Vector3Int tileLocation = Utils.WorldToTilemapPosition(worldLocation);

        PlayerController player = null;
        if (interacter is PlayerController controller)
            player = controller;
        if (interacter is KoopaWalk koopa)
            player = koopa.previousHolder;

        if (player)
        {
            if (player.state == Enums.PowerupState.MegaMushroom)
            {
                //Break

                //Tilemap
                object[] parametersTile = new object[] { tileLocation.x, tileLocation.y, null };
                GameManager.Instance.SendAndExecuteEvent(Enums.NetEventIds.SetTile, parametersTile, ExitGames.Client.Photon.SendOptions.SendReliable);

                //Particle
                object[] parametersParticle = new object[] { tileLocation.x, tileLocation.y, "BrickBreak", new Vector3(particleColor.r, particleColor.g, particleColor.b) };
                GameManager.Instance.SendAndExecuteEvent(Enums.NetEventIds.SpawnParticle, parametersParticle, ExitGames.Client.Photon.SendOptions.SendUnreliable);

                player.photonView.RPC("PlaySound", RpcTarget.All, Enums.Sounds.World_Block_Break);
                return true;
                Debug.Log("Mega?");
            }

            //Give coin to player
            player.photonView.RPC("CollectCoin", RpcTarget.All, -1, worldLocation + Vector3.one / 4f);

        }
        else
        {
            interacter.gameObject.GetPhotonView().RPC("PlaySound", RpcTarget.All, Enums.Sounds.World_Coin_Collect);
            Debug.Log("Sida");
        }

        if (remainingCoins <= 1 || remainingTime <= 0)
        {
            Debug.Log("Hola");
            Bump(interacter, direction, worldLocation);

            object[] parametersBump = new object[] { tileLocation.x, tileLocation.y, direction == InteractionDirection.Down, resultTile, "Coin" };
            GameManager.Instance.SendAndExecuteEvent(Enums.NetEventIds.BumpTile, parametersBump, ExitGames.Client.Photon.SendOptions.SendReliable);
            return false;
        }else
        {
            Bump(interacter, direction, worldLocation);

            object[] parametersBump = new object[] { tileLocation.x, tileLocation.y, direction == InteractionDirection.Down, resultTile, "Coin", x10 };
            GameManager.Instance.SendAndExecuteEvent(Enums.NetEventIds.BumpTile, parametersBump, ExitGames.Client.Photon.SendOptions.SendReliable);
            return false;


            
        }
        
    }

    IEnumerator Timer()
    {
        while (remainingTime < 0)
        {
            yield return new WaitForSeconds(1);
            remainingTime = remainingTime - 1;
        }

    }
}
