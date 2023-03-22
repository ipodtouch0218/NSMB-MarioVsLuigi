using UnityEngine;

public class SecondaryCameraRenderController : MonoBehaviour
{
    //Variable holding the Main Camera's Transfrom (the parent of the Scroll Camera)
    public Transform mainCameraTransform;

    //Stores the Max and Min values for X;
    private float minX;
    private float maxX;

    //Stores the Instance of the Camera Component on the ScrollCamera
    private Camera camera;

    public void Start()
    {
        mainCameraTransform = gameObject.transform.parent.transform;
        minX = GameManager.Instance.LevelMinX;
        maxX = GameManager.Instance.LevelMaxX;
        camera = GetComponent<Camera>();
        //Invoke this method less than a frame per second (roughly 9 times per second)
        InvokeRepeating("CameraRenderStatus", 0f, 0.11f);
    }
    public void CameraRenderStatus()
    {
        //If mainCameraTransform is between one unit behind MinX and 6 units in front of MinX OR between one unit in front of MaxX and 6 units behind MaxX, turn on Camera if not on.
        //The 1 and 7 listed below are buffer zones listed to ensure that pop-in / pop-out doesn't appear.
        if(mainCameraTransform.position.x > minX - 1 && mainCameraTransform.position.x < minX + 7 || mainCameraTransform.position.x < maxX + 1 && mainCameraTransform.position.x > maxX - 7)
        {
            if (!camera.isActiveAndEnabled)
            {
                //Turn on Camera
                camera.enabled = true;
            }
            return;
        }

        //Otherwise, turn off the Camera if not off
        if (camera.isActiveAndEnabled){
            camera.enabled = false;
        }
    }
    
}
