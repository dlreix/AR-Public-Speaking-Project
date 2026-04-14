using UnityEngine;

public class AudienceTestingUI : MonoBehaviour
{
    private Rect windowRect = new Rect(20, 20, 250, 300);

    void OnGUI()
    {
        windowRect = GUI.Window(0, windowRect, DrawUI, "Audience Procedural UI");
    }

    void DrawUI(int windowID)
    {
        GUILayout.Label("Change States Manually:");

        if (GUILayout.Button("State: Neutral (Idle)", GUILayout.Height(35))) 
            SetState(AudienceState.Neutral);
            
        if (GUILayout.Button("State: Attentive (Focus)", GUILayout.Height(35))) 
            SetState(AudienceState.Attentive);
            
        if (GUILayout.Button("State: Bored (Slouch)", GUILayout.Height(35))) 
            SetState(AudienceState.Bored);
            
        if (GUILayout.Button("State: Distracted (Look Around)", GUILayout.Height(35))) 
            SetState(AudienceState.Distracted);
            
        if (GUILayout.Button("State: Applauding (Clap)", GUILayout.Height(35))) 
            SetState(AudienceState.Applauding);

        GUILayout.Space(20);
        if (GUILayout.Button("Force Re-Spawn", GUILayout.Height(40)))
        {
            var spawner = FindFirstObjectByType<AudienceSpawner>();
            if (spawner != null) spawner.ForceSpawn();
        }

        GUI.DragWindow();
    }

    void SetState(AudienceState state)
    {
        var members = FindObjectsByType<AudienceMember>(FindObjectsSortMode.None);
        foreach (var m in members)
        {
            m.SetState(state);
        }
    }
}
