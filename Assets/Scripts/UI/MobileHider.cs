using UnityEngine;

public class MobileHider : MonoBehaviour
{
    void Start()
    {
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
}
