using UnityEngine;

namespace NSMB.Background {
    public class BoundsContainer : MonoBehaviour {

        //---Public Variables
        public Bounds Bounds => new(transform.position + bounds.center, bounds.size);

        //---Serialized Variables
        [SerializeField] private Bounds bounds;

        public void OnDrawGizmosSelected() {
            Gizmos.color = new(0, 1, 0, 0.25f);
            Gizmos.DrawCube(transform.position + bounds.center, bounds.size);
        }
    }
}
