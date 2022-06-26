using Photon.Pun;
using System.Collections.Generic;
using UnityEngine;

public class CustomAnimatorSerializer : MonoBehaviour, ICustomSerializeView {

    public bool Active { get; set; } = true;

    [SerializeField]
    private Animator animator;
    private object[] currentValues;

    public void Start() {
        currentValues = new object[animator.parameterCount];
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

    public void Serialize(List<byte> buffer) {
        for (byte i = 0; i < animator.parameterCount; i++) {
            var parameter = animator.GetParameter(i);
            object oldValue = currentValues[i];
            object newValue;

            switch (parameter.type) {
            case AnimatorControllerParameterType.Bool:
            case AnimatorControllerParameterType.Trigger:
                newValue = animator.GetBool(parameter.name);
                if ((bool) newValue == (bool) oldValue)
                    continue;

                byte newId = i;
                if ((bool) newValue)
                    newId |= 0x80;

                buffer.Add(newId);
                currentValues[i] = newValue;
                break;

            case AnimatorControllerParameterType.Int:
            case AnimatorControllerParameterType.Float:
                newValue = animator.GetFloat(parameter.name);
                //0.01 delta
                if (Mathf.Abs((float) newValue - (float) oldValue) <= 0.01)
                    continue;

                buffer.Add(i);
                SerializationUtils.PackToShort(buffer, (float) newValue, -100, 100);
                currentValues[i] = newValue;
                break;
            }
        }

        //terminator
        buffer.Add(0xFF);
    }

    public void Deserialize(List<byte> buffer, ref int index, PhotonMessageInfo info) {
        byte id;
        while ((id = buffer[index++]) != 0xFF) {
            var parameter = animator.GetParameter(id & 0x7F);

            switch (parameter.type) {
            case AnimatorControllerParameterType.Bool:
            case AnimatorControllerParameterType.Trigger:
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