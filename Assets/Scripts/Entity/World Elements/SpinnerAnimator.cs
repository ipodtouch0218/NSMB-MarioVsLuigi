using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SpinnerAnimator : MonoBehaviour {

    public Vector3 idleSpinSpeed = new Vector3(0, -100, 0), fastSpinSpeed = new Vector3(0, -1800, 0); 
    public Transform topArmBone;
    public BoxCollider2D playerCollider;

    private float spinningSpeed, spinPercentage = 0;
    private List<PlayerController> playersInside = new List<PlayerController>();

    void Update() {
        bool players = playersInside.Count >= 1;
        float percentage = 0;
        if (players) {
            float playerWorldY = float.PositiveInfinity;
            foreach (PlayerController player in playersInside) {
                if (player.body.velocity.y > 0.2f) continue;
                playerWorldY = Mathf.Min(playerWorldY, player.transform.position.y);
            }
            float spinnerWorldY = transform.position.y;

            if (playerWorldY != float.PositiveInfinity) {
                percentage = 1 - ((playerWorldY - spinnerWorldY - 0.1f) / 0.25f);
            }
        }

        spinPercentage = Mathf.Min(Mathf.Max(0, spinPercentage + (players ? 0.75f * Time.deltaTime : -1f * Time.deltaTime)), 1);

        topArmBone.eulerAngles += ((fastSpinSpeed * spinPercentage) + (idleSpinSpeed * (1-spinPercentage))) * Time.deltaTime;
        topArmBone.localPosition = new Vector3(0, percentage * -0.07f, 0);
    }

    void OnTriggerExit2D(Collider2D collider) {
        PlayerController cont = collider.gameObject.GetComponent<PlayerController>();
        if (cont) {
            playersInside.Remove(cont);
        }
    }

    void OnTriggerEnter2D(Collider2D collider) {
        PlayerController cont = collider.gameObject.GetComponent<PlayerController>();
        if (cont) {
            playersInside.Add(cont);
        }
    }
}