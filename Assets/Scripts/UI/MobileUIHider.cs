using UnityEngine;
#if !UNITY_EDITOR && UNITY_WEBGL
using System.Runtime.InteropServices;
#endif

public class MobileUIHider : MonoBehaviour
{
    void Start()
    {
#if !UNITY_EDITOR && UNITY_WEBGL
        if (MobileCheck())
            return;
#endif
        switch (Application.platform)
        {
            case RuntimePlatform.IPhonePlayer:
            case RuntimePlatform.Android:
                break;
            default:
                gameObject.SetActive(false);
                break;
        }
    }

#if !UNITY_EDITOR && UNITY_WEBGL
    [DllImport("__Internal")]
    private static extern bool MobileCheck();
#endif
}
