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
    private readonly List<byte> cachedChanges = new();

    private float[] lastSendTimestamps;

    public void Awake() {
        currentValues = new object[animator.parameterCount];
        lastSendTimestamps = new float[animator.parameterCount];
        for (int i = 0; i < animator.parameterCount; i++) {
            var parameter = animator.parameters[i];

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

        for (byte i = 0; i < animator.parameterCount; i++) {
            if (cachedChanges.Contains(i))
                continue;

            var param = animator.GetParameter(i);

            if (ignoredParameters.Contains(param.name))
                continue;

            switch (param.type) {
            case AnimatorControllerParameterType.Trigger:
                if (animator.GetBool(param.name))
                    cachedChanges.Add(i);

                break;
            case AnimatorControllerParameterType.Bool:
                bool value = animator.GetBool(param.name);
                if (value != (bool) currentValues[i])
                    cachedChanges.Add(i);

                break;
            }
        }
    }

    public void Serialize(List<byte> buffer) {
        int bufferSize = buffer.Count;

        for (byte i = 0; i < animator.parameterCount; i++) {
            var param = animator.GetParameter(i);

            if (ignoredParameters.Contains(param.name))
                continue;

            object oldValue = currentValues[i];
            object newValue;
            //occasionally resend all values, just in case.
            bool forceResend = param.type != AnimatorControllerParameterType.Trigger && PhotonNetwork.Time - lastSendTimestamps[i] > RESEND_RATE;

            switch (param.type) {
            case AnimatorControllerParameterType.Trigger:
                bool triggered = cachedChanges.Contains(i) || animator.GetBool(param.name);
                if (!triggered)
                    continue;

                cachedChanges.Remove(i);
                buffer.Add(i);
                break;

            case AnimatorControllerParameterType.Bool:
                newValue = animator.GetBool(param.name);
                bool changed = (bool) newValue != (bool) oldValue;
                bool cached = cachedChanges.Contains(i);
                if (!forceResend && !changed && !cached)
                    continue;

                if (!changed && cached)
                    newValue = !(bool) newValue;

                cachedChanges.Remove(i);

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
            var parameter = animator.GetParameter(id & 0x7F);

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