using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;

public class HideOnIdle : MonoBehaviour, IPointerMoveHandler, IPointerUpHandler
{
    //---Serialized Variables
    [SerializeField] private GameObject targetObject;
    
    //---Private Variables
    private bool isShowing = true;
    private bool isOverObject = false;
    private Coroutine hideCoroutine;
    private CanvasGroup targetCanvasGroup;

    private void Awake() {
        targetObject.TryGetComponent(out targetCanvasGroup);
        hideCoroutine = StartCoroutine(HideAfterDelay());
    }
    
    public void OnPointerMove(PointerEventData eventData) {
        if (!targetObject) return;
        
        var raycastResults = new List<RaycastResult>();
        EventSystem.current.RaycastAll(eventData, raycastResults);
        isOverObject = raycastResults.Any(result =>
            result.gameObject == targetObject || result.gameObject.transform.IsChildOf(targetObject.transform));

        SetVisibility(true);
        if (hideCoroutine != null) {
            StopCoroutine(hideCoroutine);
            hideCoroutine = null;
        }
        if(!isOverObject) hideCoroutine = StartCoroutine(HideAfterDelay());
    }

    public void OnPointerUp(PointerEventData eventData) {
        OnPointerMove(eventData);
    }
    
    private IEnumerator HideAfterDelay() {
        yield return new WaitForSeconds(2f);
        SetVisibility(false);
    }

    private void SetVisibility(bool how) {
        if (targetCanvasGroup) {
            targetCanvasGroup.alpha = how ? 1 : 0;
            targetCanvasGroup.interactable = how;
        }
        else targetObject.SetActive(how);
        isShowing = how;
    }
}
