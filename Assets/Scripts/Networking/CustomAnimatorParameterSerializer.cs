using System.Collections.Generic;
using UnityEngine;

using Photon.Pun;
using NSMB.Utils;

public class CustomAnimatorParameterSerializer : MonoBehaviourPun, ICustomSerializeView {

    private static readonly float EPSILON = 0.02f;
    private static readonly float RESEND_RATE = 0.25f;

    public bool Active { get; set; } = true;

    [SerializeField]
    private List<string> ignoredParameters;

    [SerializeField]
    private Animator animator;
    private object[] currentValues;
    private bool[] cachedChanges, disabledParameters;
    private int paramCount;
    private AnimatorControllerParameter[] parameters;

    private float[] lastSendTimestamps;

    public void Start() {
        paramCount = animator.parameterCount;
        parameters = animator.parameters;

        currentValues = new object[paramCount];
        cachedChanges = new bool[paramCount];
        disabledParameters = new bool[paramCount];
        for (int i = 0; i < paramCount; i++) {
            var param = parameters[i];
            if (ignoredParameters.Contains(param.name))
                disabledParameters[i] = true;
        }

        lastSendTimestamps = new float[paramCount];
        for (int i = 0; i < paramCount; i++) {
            var parameter = parameters[i];

            switch (parameter.type) {
            case AnimatorControllerParameterType.Bool:
            case AnimatorControllerParameterType.Trigger:
                currentValues[i] = parameter.defaultBool;
                break;

            case AnimatorControllerParameterType.Int:
            case AnimatorControllerParameterType.Float:
                currentValues[i] = parameter.defaultFloat;
                break;
            }
        }
    }

    public void Update() {
        if (!photonView.IsMine)
            return;

        for (byte i = 0; i < paramCount; i++) {
            if (disabledParameters[i] || cachedChanges[i])
                continue;

            var param = parameters[i];

            switch (param.type) {
            case AnimatorControllerParameterType.Trigger:
                if (animator.GetBool(param.name))
                    cachedChanges[i] = true;

                break;
            case AnimatorControllerParameterType.Bool:
                bool value = animator.GetBool(param.name);
                if (value != (bool) currentValues[i])
                    cachedChanges[i] = true;

                break;
            }
        }
    }

    public void Serialize(List<byte> buffer) {
        int bufferSize = buffer.Count;

        for (byte i = 0; i < paramCount; i++) {
            if (disabledParameters[i])
                continue;

            var param = parameters[i];

            object oldValue = currentValues[i];
            object newValue;
            //occasionally resend all values, just in case.
            bool forceResend = param.type != AnimatorControllerParameterType.Trigger && PhotonNetwork.Time - lastSendTimestamps[i] > RESEND_RATE;

            switch (param.type) {
            case AnimatorControllerParameterType.Trigger:
                bool triggered = cachedChanges[i] || animator.GetBool(param.name);
                if (!triggered)
                    continue;

                cachedChanges[i] = false;
                buffer.Add(i);
                break;

            case AnimatorControllerParameterType.Bool:
                newValue = animator.GetBool(param.name);
                bool changed = (bool) newValue != (bool) oldValue;
                bool cached = cachedChanges[i];
                if (!forceResend && !changed && !cached)
                    continue;

                if (!changed && cached)
                    newValue = !(bool) newValue;

                cachedChanges[i] = false;

                byte newId = i;
                if ((bool) newValue)
                    newId |= 0x80;

                buffer.Add(newId);
                currentValues[i] = newValue;
                break;

            case AnimatorControllerParameterType.Int:
            case AnimatorControllerParameterType.Float:
                newValue = animator.GetFloat(param.name);
                if (!forceResend && Mathf.Abs((float) newValue - (float) oldValue) <= EPSILON)
                    continue;

                buffer.Add(i);
                SerializationUtils.PackToShort(buffer, (float) newValue, -100, 100);
                currentValues[i] = newValue;
                break;
            }

            lastSendTimestamps[i] = (float) PhotonNetwork.Time;
        }

        //terminator
        if (buffer.Count != bufferSize)
            buffer.Add(0xFF);
    }

    public void Deserialize(List<byte> buffer, ref int index, PhotonMessageInfo info) {
        byte id;
        while ((id = buffer[index++]) != 0xFF) {
            var parameter = parameters[id & 0x7F];

            switch (parameter.type) {
            case AnimatorControllerParameterType.Trigger:
                animator.SetTrigger(parameter.name);
                break;
            case AnimatorControllerParameterType.Bool:
                //toggle
                animator.SetBool(parameter.name, Utils.BitTest(id, 7));
                break;

            case AnimatorControllerParameterType.Int:
            case AnimatorControllerParameterType.Float:
                SerializationUtils.UnpackFromShort(buffer, ref index, -100, 100, out float newValue);
                animator.SetFloat(parameter.name, newValue);
                break;
            }
        }
    }
}