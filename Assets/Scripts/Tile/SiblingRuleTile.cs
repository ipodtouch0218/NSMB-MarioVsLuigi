using System.Collections;
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

    public new bool RuleMatches(TilingRule rule, Vector3Int position, ITilemap tilemap, int angle) {
        //TODO: doesn't work.
        if (GameManager.Instance)
            Utils.WrapTileLocation(ref position);

        return base.RuleMatches(rule, position, tilemap, angle);
    }

    public new bool RuleMatches(TilingRule rule, Vector3Int position, ITilemap tilemap, bool mirrorX, bool mirrorY) {
        //TODO: doesn't work.
        if (GameManager.Instance)
            Utils.WrapTileLocation(ref position);

        return base.RuleMatches(rule, position, tilemap, mirrorX, mirrorY);
    }
}