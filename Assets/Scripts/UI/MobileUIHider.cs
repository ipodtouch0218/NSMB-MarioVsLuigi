using UnityEngine;

public class MobileUIHider : MonoBehaviour
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
