using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;
using InputDevice = UnityEngine.XR.InputDevice;
using Keyboard = UnityEngine.InputSystem.Keyboard;

namespace VRPublicSpeaking.AppShell.Presentation
{
    [DisallowMultipleComponent]
    public class PresentationInputController : MonoBehaviour
    {
        [SerializeField] private PresentationBoardController boardController;

        private readonly List<InputDevice> leftControllers = new List<InputDevice>();
        private readonly List<InputDevice> rightControllers = new List<InputDevice>();
        private bool previousLeftClick;
        private bool previousRightClick;

        public static PresentationInputController EnsureForScene(PresentationBoardController board)
        {
            if (board == null)
            {
                return null;
            }

            PresentationInputController controller =
                FindFirstObjectByType<PresentationInputController>(FindObjectsInactive.Include);
            if (controller == null)
            {
                controller = board.gameObject.AddComponent<PresentationInputController>();
            }

            controller.Configure(board);
            return controller;
        }

        public void Configure(PresentationBoardController board)
        {
            boardController = board;
            RefreshControllerDevices();
        }

        private void OnEnable()
        {
            RefreshControllerDevices();
        }

        private void Update()
        {
            if (boardController == null || !boardController.HasDeck)
            {
                return;
            }

            if (!HasValidController(leftControllers) && !HasValidController(rightControllers))
            {
                RefreshControllerDevices();
            }

            if (WasNextPressed())
            {
                boardController.NextPage();
            }

            if (WasPreviousPressed())
            {
                boardController.PreviousPage();
            }
        }

        private bool WasNextPressed()
        {
            bool keyboardPressed =
                Keyboard.current != null &&
                (Keyboard.current.rightArrowKey.wasPressedThisFrame ||
                 Keyboard.current.pageDownKey.wasPressedThisFrame);
            return keyboardPressed || WasControllerClickPressed(rightControllers, ref previousRightClick);
        }

        private bool WasPreviousPressed()
        {
            bool keyboardPressed =
                Keyboard.current != null &&
                (Keyboard.current.leftArrowKey.wasPressedThisFrame ||
                 Keyboard.current.pageUpKey.wasPressedThisFrame);
            return keyboardPressed || WasControllerClickPressed(leftControllers, ref previousLeftClick);
        }

        private static bool WasControllerClickPressed(List<InputDevice> devices, ref bool previousPressed)
        {
            bool pressed = false;
            for (int index = 0; index < devices.Count; index++)
            {
                InputDevice device = devices[index];
                if (!device.isValid)
                {
                    continue;
                }

                if (device.TryGetFeatureValue(CommonUsages.primary2DAxisClick, out bool value) && value)
                {
                    pressed = true;
                    break;
                }
            }

            bool triggered = pressed && !previousPressed;
            previousPressed = pressed;
            return triggered;
        }

        private static bool HasValidController(List<InputDevice> devices)
        {
            for (int index = 0; index < devices.Count; index++)
            {
                if (devices[index].isValid)
                {
                    return true;
                }
            }

            return false;
        }

        private void RefreshControllerDevices()
        {
            leftControllers.Clear();
            rightControllers.Clear();
            InputDevices.GetDevicesWithCharacteristics(
                InputDeviceCharacteristics.Controller | InputDeviceCharacteristics.Left,
                leftControllers);
            InputDevices.GetDevicesWithCharacteristics(
                InputDeviceCharacteristics.Controller | InputDeviceCharacteristics.Right,
                rightControllers);
        }
    }
}
