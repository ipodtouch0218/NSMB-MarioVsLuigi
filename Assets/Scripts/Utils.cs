using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Photon.Realtime;
using UnityEngine.Tilemaps;

public class Utils {
    
    public static RaiseEventOptions EVENT_OTHERS {get;} = new RaiseEventOptions{Receivers=ReceiverGroup.Others};
    public static RaiseEventOptions EVENT_ALL {get;} = new RaiseEventOptions{Receivers=ReceiverGroup.All};
    public static Vector3Int WorldToTilemapPosition(Vector3 vec) {
        return WorldToTilemapPosition(vec.x, vec.y);
    }

    public static Vector3Int WorldToTilemapPosition(float worldX, float worldY) {
        Transform tilemap = GameManager.Instance.tilemap.transform;

        int x = Mathf.FloorToInt((worldX - tilemap.position.x) / tilemap.localScale.x);
        int y = Mathf.FloorToInt((worldY - tilemap.position.y) / tilemap.localScale.y);

        return new Vector3Int(x, y, 0);
    }

    public static Vector3 TilemapToWorldPosition(Vector3Int tilevec) {
        return TilemapToWorldPosition(tilevec.x, tilevec.y);
    }

    public static Vector3 TilemapToWorldPosition(int tileX, int tileY) {
        Transform tilemap = GameManager.Instance.tilemap.transform;

        float x = (tileX * tilemap.localScale.x) + tilemap.position.x;
        float y = (tileY * tilemap.localScale.y) + tilemap.position.y;

        return new Vector3(x, y, 0);
    }
}