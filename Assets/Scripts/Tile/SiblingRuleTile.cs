using NSMB.Utils;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

// https://github.com/Unity-Technologies/2d-extras/issues/67#issuecomment-616013310
[ExecuteInEditMode]
public class SiblingRuleTile : RuleTile {
    public List<TileBase> siblings;
    public override bool RuleMatch(int neighbor, TileBase other) {
        return neighbor switch {
            TilingRuleOutput.Neighbor.This => siblings.Contains(other) || base.RuleMatch(neighbor, other),
            TilingRuleOutput.Neighbor.NotThis => !siblings.Contains(other) && base.RuleMatch(neighbor, other),
            _ => base.RuleMatch(neighbor, other),
        };
    }
    public override Vector3Int GetOffsetPosition(Vector3Int position, Vector3Int offset) {
        Vector3Int result = position + offset;
        if (!GameManager.Instance)
            return result;

        Utils.WrapTileLocation(ref result);
        return result;
    }

    public override Vector3Int GetOffsetPositionReverse(Vector3Int position, Vector3Int offset) {
        Vector3Int result = position - offset;
        if (!GameManager.Instance)
            return result;

        Utils.WrapTileLocation(ref result);
        return result;
    }
}