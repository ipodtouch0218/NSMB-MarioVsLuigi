// ---------------------------------------------------------------------------------------------
// <copyright>PhotonNetwork Framework for Unity - Copyright (C) 2020 Exit Games GmbH</copyright>
// <author>developer@exitgames.com</author>
// ---------------------------------------------------------------------------------------------

using System.Collections.Generic;
using UnityEngine;

namespace Fusion.Editor {
  public static class BoundsTools {
    public enum BoundsType { Both, MeshRenderer, Collider, Manual }

    // 3d
    private static readonly List<MeshFilter> meshFilters = new List<MeshFilter>();
    private static readonly List<Renderer> meshRenderers = new List<Renderer>();
    private static readonly List<Collider> colliders = new List<Collider>();
    private static readonly List<Collider> validColliders = new List<Collider>();

    // 2d
    private static readonly List<SpriteRenderer> spriteRenderers = new List<SpriteRenderer>();
#if !DISABLE_PHYSICS_2D
    private static readonly List<Collider2D> colliders2D = new List<Collider2D>();
    private static readonly List<Collider2D> validColliders2D = new List<Collider2D>();
#endif
    /// <summary>
    /// Collect the bounds of the indicated types (MeshRenderer and/or Collider) on the object and all of its children, and returns bounds that are a sum of all of those.
    /// </summary>
    /// <param name="go">GameObject to start search from.</param>
    /// <param name="factorIn">The types of bounds to factor in.</param>
    /// <param name="includeChildren">Whether to search all children for bounds.</param>
    /// <returns></returns>
    public static Bounds CollectMyBounds(this GameObject go, BoundsType factorIn, out int numOfBoundsFound, bool includeChildren = true, bool includeInactive = false) {
      // if we are ignoring inactive, an inactive parent is already a null. Quit here.
      if (!go.activeInHierarchy && !!includeInactive) {
        numOfBoundsFound = 0;
        return new Bounds();
      }

      bool bothtype = factorIn == BoundsType.Both;
      bool rendtype = bothtype || factorIn == BoundsType.MeshRenderer;
      bool colltype = bothtype || factorIn == BoundsType.Collider;

      // Clear the reusables so they have counts of zero
      meshFilters.Clear();
      meshRenderers.Clear();
      colliders.Clear();
      spriteRenderers.Clear();
      validColliders.Clear();
#if !DISABLE_PHYSICS_2D
      validColliders2D.Clear();
#endif
      int myBoundsCount = 0;

      // Find all of the MeshRenderers and Colliders (as specified)
      if (rendtype) {
        if (go.activeInHierarchy) {
          if (includeChildren) {
            go.GetComponentsInChildren(includeInactive, meshRenderers);
            go.GetComponentsInChildren(includeInactive, meshFilters);
            go.GetComponentsInChildren(includeInactive, spriteRenderers);
          } else {
            go.GetComponents(meshRenderers);
            go.GetComponents(meshFilters);
            go.GetComponents(spriteRenderers);
          }
        }
      }

      if (colltype) {
        if (go.activeInHierarchy) {
          if (includeChildren) {
            go.GetComponentsInChildren(includeInactive, colliders);
#if !DISABLE_PHYSICS_2D
            go.GetComponentsInChildren(includeInactive, colliders2D);
#endif

          } else {
            go.GetComponents(colliders);
#if !DISABLE_PHYSICS_2D
            go.GetComponents(colliders2D);
#endif
          }
        }
      }

      // Add any MeshRenderer attached to the found MeshFilters to their own list.
      // We want the MeshRenderer for its bounds, but only if there is a MeshFilter, otherwise there is a risk of a 0,0,0
      for (int i = 0; i < meshFilters.Count; i++) {
        Renderer mr = meshFilters[i].GetComponent<Renderer>();

        if (mr && (mr.enabled || includeInactive)) {
          if (!meshRenderers.Contains(mr))
            meshRenderers.Add(mr);
        }
      }

      // Collect only the valid colliders (ignore inactive if not includeInactive)
      for (int i = 0; i < colliders.Count; i++) {
        if (colliders[i].enabled || includeInactive)
          if (colliders[i])
            validColliders.Add(colliders[i]);
      }

#if !DISABLE_PHYSICS_2D

      // Collect only the valid colliders (ignore inactive if not includeInactive)
      for (int i = 0; i < colliders2D.Count; i++) {
        if (colliders2D[i] && colliders2D[i].enabled || includeInactive)
          // 2d colliders arrive as null but present in scene changes, test for null
          if (colliders2D[i])
            validColliders2D.Add(colliders2D[i]);
      }
#endif
      // Make sure we found some bounds objects, or we need to quit.
      numOfBoundsFound =
        meshRenderers.Count +
        spriteRenderers.Count +
        validColliders.Count
#if !DISABLE_PHYSICS_2D
        + validColliders2D.Count
#endif
        ;
      // No values means no bounds will be found, and this will break things if we try to use it.
      if (numOfBoundsFound == 0) {
        return new Bounds();
      }

      // Get a starting bounds. We need this because the default of centered 0,0,0 will break things if the map is
      // offset and doesn't encapsulate the world origin.
      Bounds compositeBounds;

      if (meshRenderers.Count > 0)
        compositeBounds = meshRenderers[0].bounds;

      else if (validColliders.Count > 0)
        compositeBounds = validColliders[0].bounds;

#if !DISABLE_PHYSICS_2D
      else if (validColliders2D.Count > 0 && validColliders2D[0])
        compositeBounds = validColliders2D[0].bounds;
#endif
      else if (spriteRenderers.Count > 0)
        compositeBounds = spriteRenderers[0].bounds;
      /// nothing found, return an empty bounds
      else
        return new Bounds();

      for (int i = 0; i < spriteRenderers.Count; i++) {
        myBoundsCount++;
        compositeBounds.Encapsulate(spriteRenderers[i].bounds);
      }

      // Encapsulate all outer found bounds into that. We will be adding the root to itself, but no biggy, this only runs once.
      for (int i = 0; i < meshRenderers.Count; i++) {
        myBoundsCount++;
        compositeBounds.Encapsulate(meshRenderers[i].bounds);
      }

      for (int i = 0; i < validColliders.Count; i++) {
        myBoundsCount++;
        compositeBounds.Encapsulate(validColliders[i].bounds);
      }
#if !DISABLE_PHYSICS_2D
      for (int i = 0; i < validColliders2D.Count; i++) {
        myBoundsCount++;
        if (validColliders2D[i])
          compositeBounds.Encapsulate(validColliders2D[i].bounds);
      }
#endif
      return compositeBounds;

    }

    public static Bounds CollectMyBounds(GameObject go, BoundsType factorIn, bool includeChildren = true) {
      int dummy;
      return CollectMyBounds(go, factorIn, out dummy, includeChildren);
    }

  }
}

