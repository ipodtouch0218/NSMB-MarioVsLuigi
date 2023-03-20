using UnityEngine;

public class ScrollCameraToggle : MonoBehaviour
{
    //The scroll camera being stored
    public Camera scrollCamera;

    //Invisible Renderer only for checking purposes (set to Sprite Renderer of the Object)
    public Renderer renderCheck;

    //Other ScrollCameraToggleGameObject
    public ScrollCameraToggle otherScrollChecker;
    
    void Start()
    {
        //At the start, assign the Base & Main Cameras.
        scrollCamera = GameObject.Find("ScrollCamera").GetComponent<Camera>();
    }

    // Update is called once per frame
    void Update()
    {
        //If the object is not visible, then disable the ScrollCamera Camera component
		if (renderCheck.isVisible == false && otherScrollChecker.renderCheck.isVisible == false)
		{
			if (scrollCamera.isActiveAndEnabled)
			{
                scrollCamera.enabled = false;
            }
            return;
		}

        //Else, re-enable Camera Component
		if (scrollCamera.isActiveAndEnabled == false)
		{
            scrollCamera.enabled = true;
		}
    }
}
