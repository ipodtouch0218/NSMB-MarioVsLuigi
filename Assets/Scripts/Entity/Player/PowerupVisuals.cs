using Quantum;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace NSMB.Entities.Player {
    [Serializable]
    public class PowerupVisuals {

        //---Static Variables
        #region Shader Properties
        private static readonly int MainTex = Shader.PropertyToID("_MainTex");
        private static readonly int OverallsMask = Shader.PropertyToID("_OverallsMask");
        private static readonly int ShirtMask = Shader.PropertyToID("_ShirtMask");
        private static readonly int CapMask = Shader.PropertyToID("_CapMask");
        #endregion

        //---Serialized Variables
        public PowerupState State;
        public List<GameObject> AdditionalGameObjects;

        [Header("Model")]
        public GameObject BaseModel;
        public Vector3 ModelScale = Vector3.one;
        public float ModelHeightInBlocks = 1f;

        [Header("Material")]
        public MaterialTextureReplacement[] TextureReplacements;

        [Header("Animation")]
        public Avatar AnimationAvatar;
        public RuntimeAnimatorController AnimatorOverrides;

        public void InitializeMaterials(Dictionary<Material, Material> replacements) {
            foreach (var textureReplacement in TextureReplacements) {
                if (replacements.TryGetValue(textureReplacement.Material, out var mat)) {
                    textureReplacement.Material = mat;
                }
            }
        }

        public void ApplyTextureReplacements(bool starman) {
            foreach (var replacement in TextureReplacements) {
                Material material = replacement.Material;
                material.SetTexture(MainTex, (starman && replacement.StarmanAlbedoTexture) ? replacement.StarmanAlbedoTexture : replacement.AlbedoTexture);
                material.SetTexture(OverallsMask, replacement.OverallsMaskTexture);
                Debug.Log($"{material.name} OverallsMask set to {replacement.OverallsMaskTexture}");
                material.SetTexture(ShirtMask, replacement.ShirtMaskTexture);
                material.SetTexture(CapMask, replacement.CapMaskTexture);
            }
        }

        public void SwapAnimations(MarioPlayerAnimator marioAnimator) {
            if (AnimationAvatar != marioAnimator.Animator.avatar) {
                return;
            }

            // Preserve Animations
            int[] layers = { 0, 1, 3 };
            AnimatorStateInfo[] layerInfo = new AnimatorStateInfo[marioAnimator.Animator.layerCount];
            foreach (int i in layers) {
                layerInfo[i] = marioAnimator.Animator.GetCurrentAnimatorStateInfo(i);
            }

            marioAnimator.Animator.avatar = AnimationAvatar;
            marioAnimator.Animator.runtimeAnimatorController = AnimatorOverrides;

            // Push back state 
            marioAnimator.Animator.Rebind();

            foreach (int i in layers) {
                marioAnimator.Animator.Play(layerInfo[i].fullPathHash, i, layerInfo[i].normalizedTime);
            }
        }

        public void EnableProps() {
            foreach (var gameObject in AdditionalGameObjects) {
                gameObject.SetActive(true);
            }
        }

        public void DisableProps() {
            foreach (var gameObject in AdditionalGameObjects) {
                gameObject.SetActive(false);
            }
        }

        public void EnableModel() {
            BaseModel.SetActive(true);
        }

        public void DisableModel() {
            BaseModel.SetActive(false);
        }


        [Serializable]
        public class MaterialTextureReplacement {
            public Material Material;
            public Texture AlbedoTexture, OverallsMaskTexture, ShirtMaskTexture, CapMaskTexture;
            public Texture StarmanAlbedoTexture;
        }
    }
}