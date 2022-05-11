using UnityEngine;
using UnityEngine.Tilemaps;
using Photon.Pun;

public class BlockBump : MonoBehaviour {
    public string resultTile = "";
    public SpawnResult spawn = SpawnResult.Nothing;
    public Sprite sprite;
    public bool fromAbove;
    SpriteRenderer sRenderer;
    public PlayerController hitter;
    void Start() {
        Animator anim = GetComponentInChildren<Animator>();
        sRenderer = GetComponentInChildren<SpriteRenderer>();
        sRenderer.sprite = sprite;
        anim.SetBool("down", fromAbove);
        anim.SetTrigger("start");

        if (spawn == SpawnResult.Coin) {
            GameObject coin = (GameObject) Instantiate(Resources.Load("Prefabs/Particle/CoinFromBlock"), transform.position + new Vector3(0,(fromAbove ? -0.25f : 0.5f)), Quaternion.identity);
            coin.GetComponentInChildren<Animator>().SetBool("down", fromAbove);
        }
    }
    
    public void Kill() {
        Destroy(gameObject);
        Destroy(transform.parent.gameObject);
        
        Tilemap tm = GameManager.Instance.tilemap;
        Vector3Int loc = Utils.WorldToTilemapPosition(transform.position);
        
        Object tile = Resources.Load("Tilemaps/Tiles/" + resultTile);
        if (tile is AnimatedTile animatedTile) {
            tm.SetTile(loc, animatedTile);
        } else if (tile is Tile normalTile) {
            tm.SetTile(loc, normalTile);
        }

        if (!PhotonNetwork.IsMasterClient)
            return;

        switch(spawn) {        
            case SpawnResult.Mushroom: {
                PhotonNetwork.Instantiate("Prefabs/Powerup/Mushroom", transform.position + new Vector3(0, fromAbove ? -0.5f : 0.25f), Quaternion.identity);
                break;
            }
            case SpawnResult.FireFlower: {
                PhotonNetwork.Instantiate("Prefabs/Powerup/FireFlower", transform.position + new Vector3(0, fromAbove ? -0.5f : 0.25f), Quaternion.identity);
                break;
            }
            case SpawnResult.BlueShell: {
                PhotonNetwork.Instantiate("Prefabs/Powerup/BlueShell", transform.position + new Vector3(0, fromAbove ? -0.5f : 0.25f), Quaternion.identity);
                break;
            }
            case SpawnResult.Star: {
                PhotonNetwork.Instantiate("Prefabs/Powerup/Star", transform.position + new Vector3(0, fromAbove ? -0.5f : 0.25f), Quaternion.identity);
                break;
            }
        }
    }

    public enum SpawnResult {
        Mushroom, FireFlower, BlueShell, Star, Coin, Nothing
    }
}
