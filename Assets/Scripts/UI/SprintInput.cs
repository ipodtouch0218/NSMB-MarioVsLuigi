using UnityEngine.InputSystem.Layouts;
using UnityEngine;

[RequireComponent(typeof(CustomStick))]
public class SprintInput : UnityEngine.InputSystem.OnScreen.OnScreenControl
{
    [HideInInspector]
    public CustomStick stick;

    [InputControl(layout = "Button")]
    [SerializeField]
    private string m_ControlPath;

    protected override string controlPathInternal { get => m_ControlPath; set => m_ControlPath = value; }

    void Start()
    {
        stick = GetComponent<CustomStick>();
    }

    void Update()
    {
        if (stick.lastPos.x >= 0.7 || stick.lastPos.x <= -0.7)
        {
            SendValueToControl(1.0f);
        }
        else
        {
            SendValueToControl(0.0f);
        }
    }
}