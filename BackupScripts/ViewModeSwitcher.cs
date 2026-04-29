using UnityEngine;
using UnityEngine.InputSystem;

public class ViewModeSwitcher : MonoBehaviour
{
    public Camera presenterCamera;
    public Camera[] audienceCameras;
    public PlayerController playerController;

    void Start()
    {
        ActivatePresenterMode();
    }

    void Update()
    {
        if (Keyboard.current == null)
            return;

        if (Keyboard.current.digit1Key.wasPressedThisFrame)
        {
            ActivatePresenterMode();
        }

        if (Keyboard.current.digit2Key.wasPressedThisFrame)
        {
            ActivateAudienceCamera(0);
        }

        if (Keyboard.current.digit3Key.wasPressedThisFrame)
        {
            ActivateAudienceCamera(1);
        }

        if (Keyboard.current.digit4Key.wasPressedThisFrame)
        {
            ActivateAudienceCamera(2);
        }
    }

    void ActivatePresenterMode()
    {
        if (presenterCamera != null)
            presenterCamera.enabled = true;

        if (audienceCameras != null)
        {
            foreach (Camera cam in audienceCameras)
            {
                if (cam != null)
                    cam.enabled = false;
            }
        }

        if (playerController != null)
        {
            playerController.movementEnabled = true;
            playerController.lookEnabled = true;
        }

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        Debug.Log("Presenter Mode Active");
    }

    void ActivateAudienceCamera(int index)
    {
        if (presenterCamera != null)
            presenterCamera.enabled = false;

        if (audienceCameras != null)
        {
            for (int i = 0; i < audienceCameras.Length; i++)
            {
                if (audienceCameras[i] != null)
                    audienceCameras[i].enabled = (i == index);
            }
        }

        if (playerController != null)
        {
            playerController.movementEnabled = false;
            playerController.lookEnabled = false;
        }

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        Debug.Log("Audience Camera " + (index + 1) + " Active");
    }
}