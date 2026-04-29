using UnityEngine;
using UnityEngine.InputSystem;

public class ModeSpawnSwitcher : MonoBehaviour
{
    public Transform playerRoot;

    [Header("Presenter")]
    public Transform presenterReturnPoint;

    [Header("Audience Camera Points")]
    public Transform[] audiencePoints;

    private CharacterController playerCC;
    private PlayerController playerController;

    void Start()
    {
        if (playerRoot != null)
        {
            playerCC = playerRoot.GetComponent<CharacterController>();
            playerController = playerRoot.GetComponent<PlayerController>();
        }
    }

    void Update()
    {
        if (Keyboard.current == null)
            return;

        // 1 = Presenter Mode
        if (Keyboard.current.digit1Key.wasPressedThisFrame)
        {
            SwitchToPresenterMode();
        }

        // 2 = Audience Point 1
        if (Keyboard.current.digit2Key.wasPressedThisFrame)
        {
            SwitchToAudiencePoint(0);
        }

        // 3 = Audience Point 2
        if (Keyboard.current.digit3Key.wasPressedThisFrame)
        {
            SwitchToAudiencePoint(1);
        }

        // 4 = Audience Point 3
        if (Keyboard.current.digit4Key.wasPressedThisFrame)
        {
            SwitchToAudiencePoint(2);
        }
    }

    void SwitchToPresenterMode()
    {
        if (presenterReturnPoint != null)
        {
            MovePlayerTo(presenterReturnPoint);
        }

        if (playerController != null)
        {
            playerController.movementEnabled = true;
        }

        Debug.Log("Presenter Mode");
    }

    void SwitchToAudiencePoint(int index)
    {
        if (audiencePoints == null || index < 0 || index >= audiencePoints.Length || audiencePoints[index] == null)
        {
            Debug.LogWarning("Audience point eksik veya atanmadý.");
            return;
        }

        MovePlayerTo(audiencePoints[index]);

        if (playerController != null)
        {
            playerController.movementEnabled = false;
        }

        Debug.Log("Audience Camera Point " + (index + 1));
    }

    void MovePlayerTo(Transform targetPoint)
    {
        if (playerRoot == null || targetPoint == null)
        {
            Debug.LogWarning("PlayerRoot veya target point atanmadý.");
            return;
        }

        if (playerCC == null)
        {
            playerCC = playerRoot.GetComponent<CharacterController>();
        }

        if (playerCC != null)
            playerCC.enabled = false;

        playerRoot.position = targetPoint.position;
        playerRoot.rotation = targetPoint.rotation;

        if (playerCC != null)
            playerCC.enabled = true;
    }
}