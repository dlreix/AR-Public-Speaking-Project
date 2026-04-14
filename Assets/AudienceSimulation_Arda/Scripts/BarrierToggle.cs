using UnityEngine;
using UnityEngine.InputSystem;

public class BarrierToggle : MonoBehaviour
{
    public Collider[] barrierColliders;

    private bool barriersActive = true;

    void Start()
    {
        SetBarriers(true);
    }

    void Update()
    {
        if (Keyboard.current != null && Keyboard.current.bKey.wasPressedThisFrame)
        {
            barriersActive = !barriersActive;
            SetBarriers(barriersActive);
            Debug.Log("Barriers active: " + barriersActive);
        }
    }

    void SetBarriers(bool state)
    {
        foreach (Collider col in barrierColliders)
        {
            if (col != null)
                col.enabled = state;
        }
    }
}