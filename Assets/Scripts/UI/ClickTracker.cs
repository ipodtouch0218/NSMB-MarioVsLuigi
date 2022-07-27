/*using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.Layouts;

namespace UnityEngine.InputSystem.OnScreen
{
#if UNITY_EDITOR
using UnityEditor;
#endif
    public class ClickTracker : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        public string buttonName = "";
        public bool isJoystick = false;
        public float movementLimit = 1;
        public float movementThreshold = 0.1f;
        [AddComponentMenu("Input/On-Screen Button")]

        RectTransform rt;
        Vector3 startPos;
        Vector2 clickPos;

        Vector2 inputAxis = Vector2.zero;
        bool holding = false;
        bool clicked = false;


        ////TODO: pressure support
        /*
        /// <summary>
        /// If true, the button's value is driven from the pressure value of touch or pen input.
        /// </summary>
        /// <remarks>
        /// This essentially allows having trigger-like buttons as on-screen controls.
        /// </remarks>
        [SerializeField] private bool m_UsePressure;
        *//*

        [InputControl(layout = "Button")]
        [SerializeField]


        void Start()
        {
            MobileControls.instance.AddButton(this);

            rt = GetComponent<RectTransform>();
            startPos = rt.anchoredPosition3D;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            Debug.Log(this.gameObject.name + " Was Pressed");

            SendValueToControl(1.0f);

            if (!isJoystick)
            {
                clicked = true;
                StartCoroutine(StopClickEvent());
            }
            else
            {
                clickPos = eventData.pressPosition;
            }
        }

        WaitForEndOfFrame waitForEndOfFrame = new WaitForEndOfFrame();

        IEnumerator StopClickEvent()
        {
            yield return waitForEndOfFrame;

            clicked = false;
        }

        public void OnDrag(PointerEventData eventData)
        {

            if (isJoystick)
            {
                Vector3 movementVector = Vector3.ClampMagnitude((eventData.position - clickPos) / MobileControls.instance.canvas.scaleFactor, (rt.sizeDelta.x * movementLimit) + (rt.sizeDelta.x * movementThreshold));
                Vector3 movePos = startPos + movementVector;
                rt.anchoredPosition = movePos;

                float inputX = 0;
                float inputY = 0;
                if (Mathf.Abs(movementVector.x) > rt.sizeDelta.x * movementThreshold)
                {
                    inputX = (movementVector.x - (rt.sizeDelta.x * movementThreshold * (movementVector.x > 0 ? 1 : -1))) / (rt.sizeDelta.x * movementLimit);
                }
                if (Mathf.Abs(movementVector.y) > rt.sizeDelta.x * movementThreshold)
                {
                    inputY = (movementVector.y - (rt.sizeDelta.x * movementThreshold * (movementVector.y > 0 ? 1 : -1))) / (rt.sizeDelta.x * movementLimit);
                }
                inputAxis = new Vector2(inputX, inputY);
            }
        }


        public void OnPointerUp(PointerEventData eventData)
        {

            SendValueToControl(0.0f);

            if (isJoystick)
            {
                rt.anchoredPosition = startPos;
                inputAxis = Vector2.zero;
            }
        }

        public Vector2 GetInputAxis()
        {
            return inputAxis;
        }

        public bool GetClickedStatus()
        {
            return clicked;
        }

        public bool GetHoldStatus()
        {
            return holding;
        }
    }
#if UNITY_EDITOR
[CustomEditor(typeof(ClickTracker))]
public class ClickTracker_Editor : Editor
{
    public override void OnInspectorGUI()
    {
        ClickTracker script = (ClickTracker)target;

        script.buttonName = EditorGUILayout.TextField("Button Name", script.buttonName);
        script.isJoystick = EditorGUILayout.Toggle("Is Joystick", script.isJoystick);
        if (script.isJoystick)
        {
            script.movementLimit = EditorGUILayout.FloatField("Movement Limit", script.movementLimit);
            script.movementThreshold = EditorGUILayout.FloatField("Movement Threshold", script.movementThreshold);
        }
    }
}
#endif

}*/
