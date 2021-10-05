using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;
using Photon.Pun;

public class BlockBump : MonoBehaviour {
    public string resultTile = "";
    public SpawnResult spawn = SpawnResult.Nothing;
    public Sprite sprite;
    public bool fromAbove;
    new AudioSource audio;
    new SpriteRenderer renderer;
    public PlayerController hitter;
    void Start() {
        Animator anim = GetComponentInChildren<Animator>();
        renderer = GetComponentInChildren<SpriteRenderer>();
        renderer.sprite = sprite;
        anim.SetBool("down", fromAbove);
        anim.SetTrigger("start");

        if (spawn == SpawnResult.Coin) {
            GameObject coin = (GameObject) GameObject.Instantiate(Resources.Load("CoinFromBlock"), transform.position + new Vector3(0,(fromAbove ? -0.25f : 0.5f)), Quaternion.identity);
            coin.GetComponentInChildren<Animator>().SetBool("down", fromAbove);
        }
    }
    
    public void Kill() {
        GameObject.Destroy(gameObject);
        GameObject.Destroy(transform.parent.gameObject);
        
        Tilemap tm = GameManager.Instance.tilemap;
        Transform tmtf = tm.transform;
        Vector3Int loc = new Vector3Int(Mathf.FloorToInt((transform.position.x - tmtf.position.x) / tmtf.localScale.x), Mathf.FloorToInt((transform.position.y - tmtf.position.y) / tmtf.localScale.y), 0);
        
        Object tile = Resources.Load("Tilemaps/Tiles/" + resultTile);
        if (tile is AnimatedTile) {
            tm.SetTile(loc, (AnimatedTile) tile);
        } else {
            tm.SetTile(loc, (Tile) tile);
        }

        if (!PhotonNetwork.IsMasterClient) {
            return;
        }
        switch(spawn) {
            case SpawnResult.Mushroom: {
                PhotonNetwork.Instantiate("Mushroom", transform.position + new Vector3(0,(fromAbove ? -0.5f : 0.25f)), Quaternion.identity);
                break;
            }
            case SpawnResult.FireFlower: {
                PhotonNetwork.Instantiate("FireFlower", transform.position + new Vector3(0,(fromAbove ? -0.5f : 0.25f)), Quaternion.identity);
                break;
            }
            case SpawnResult.Coin: {
                //coin already spawned
                break;
            }
        }
    }

    public enum SpawnResult {
        Nothing, Coin, Mushroom, FireFlower 
    }
}
