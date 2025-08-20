using System.Collections.Generic;
using UnityEngine;

namespace NSMB.Utilities.Components {
    public class DisableIfWebGL : MonoBehaviour {

        //---Serialized Variables
        [SerializeField] private List<GameObject> objects;

        public void OnEnable() {
#if UNITY_WEBGL
            foreach (var obj in objects) {
                obj.SetActive(false);
            }
#endif
        }
    }
}