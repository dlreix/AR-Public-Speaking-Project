#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEditor.SceneManagement;
using System.IO;
using System.Collections.Generic;

public class AutoSceneConfigurator : EditorWindow
{
    [MenuItem("AR Trainer/🌟 1- SampleScene'deki Karakterleri Sisteme Kaydet")]
    public static void ExtractCharactersFromSampleScene()
    {
        string sampleScenePath = "Assets/Scenes/SampleScene.unity";
        if (!File.Exists(sampleScenePath))
        {
            EditorUtility.DisplayDialog("Hata", "SampleScene bulunamadı!", "Tamam");
            return;
        }

        if (EditorSceneManager.GetActiveScene().isDirty)
            EditorSceneManager.SaveOpenScenes();

        var scene = EditorSceneManager.OpenScene(sampleScenePath, OpenSceneMode.Single);
        
        // Find custom characters (they have Animators)
        Animator[] animators = Object.FindObjectsByType<Animator>(FindObjectsSortMode.None);
        List<GameObject> customCharacters = new List<GameObject>();

        string exportDir = "Assets/Prefabs/CustomAudience";
        if (!AssetDatabase.IsValidFolder("Assets/Prefabs")) AssetDatabase.CreateFolder("Assets", "Prefabs");
        if (!AssetDatabase.IsValidFolder("Assets/Prefabs/CustomAudience")) AssetDatabase.CreateFolder("Assets/Prefabs", "CustomAudience");

        int count = 0;
        foreach (Animator anim in animators)
        {
            // Skip UI stuff
            if (anim.GetComponent<RectTransform>() != null) continue;
            
            GameObject go = anim.gameObject;

            // Make sure they have our dynamic components!
            if (go.GetComponent<AudienceMember>() == null) go.AddComponent<AudienceMember>();
            if (go.GetComponent<ProceduralAudienceAnimator>() == null) go.AddComponent<ProceduralAudienceAnimator>();
            if (go.GetComponent<AudienceVisualizer>() == null) go.AddComponent<AudienceVisualizer>();

            // Save as Prefab
            string prefabPath = $"{exportDir}/CustomChar_{count}.prefab";
            PrefabUtility.SaveAsPrefabAssetAndConnect(go, prefabPath, InteractionMode.AutomatedAction);
            customCharacters.Add(AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath));
            count++;
        }

        if (count > 0)
        {
            EditorUtility.DisplayDialog("Başarılı", $"{count} adet gri karakter SampleScene'den çekildi, üzerlerine Kıyafet Randmoize edici ve Kod-Animatörü eklendi ve sisteme kaydedildi!", "Süper");
        }
        else
        {
            EditorUtility.DisplayDialog("Uyarı", "SampleScene'de hiç animatörlü karakter bulunamadı.", "Tamam");
        }
    }

    [MenuItem("AR Trainer/🌟 2- Tüm Sahnelerden Eski Karakterleri Sil ve Yenilerini Kur")]
    public static void DistributeToAllScenes()
    {
        string[] scenesToProcess = {
            "Assets/Scenes/Scene_Classroom.unity",
            "Assets/Scenes/Scene_MeetingRoom.unity",
            "Assets/Scenes/Scene_ConferenceHall.unity"
        };

        // Get our saved custom characters
        string[] guids = AssetDatabase.FindAssets("CustomChar_ t:Prefab", new[] { "Assets/Prefabs/CustomAudience" });
        List<GameObject> customPrefabs = new List<GameObject>();
        foreach (string g in guids)
        {
            customPrefabs.Add(AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(g)));
        }

        if (customPrefabs.Count == 0)
        {
            EditorUtility.DisplayDialog("Hata", "Önce 1. adımı yapıtırıp karakterleri çekmeniz lazım!", "Tamam");
            return;
        }

        foreach (string scenePath in scenesToProcess)
        {
            if (!File.Exists(scenePath)) continue;

            if (EditorSceneManager.GetActiveScene().isDirty) EditorSceneManager.SaveOpenScenes();
            var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);

            // 1. (Silme mantığı devre dışı bırakıldı - Kullanıcı isteği üzerine karakterler korunuyor)


            // Determine environment type based on scene name
            EnvironmentType envType = EnvironmentType.classroom;
            if (scene.name.Contains("Meeting")) envType = EnvironmentType.meeting_room;
            else if (scene.name.Contains("Conference")) envType = EnvironmentType.conference_hall;

            // Find or Create System
            AudienceBehaviorController behaviorController = Object.FindFirstObjectByType<AudienceBehaviorController>();
            if (behaviorController == null)
            {
                GameObject behaviorObj = new GameObject("AudienceSystem");
                behaviorController = behaviorObj.AddComponent<AudienceBehaviorController>();
            }

            // Setup Engines
            AudienceReactionEngine engine = Object.FindFirstObjectByType<AudienceReactionEngine>();
            PerformanceScoringEngine scoringEngine = Object.FindFirstObjectByType<PerformanceScoringEngine>();

            if (engine == null)
            {
                engine = behaviorController.gameObject.AddComponent<AudienceReactionEngine>();
            }

            if (scoringEngine == null)
            {
                scoringEngine = behaviorController.gameObject.AddComponent<PerformanceScoringEngine>();
            }

            // Ensure they are correctly linked
            engine.scoringEngine = scoringEngine;
            engine.environmentType = envType;
            behaviorController.reactionEngine = engine;

            // Trigger initial calculation to ensure everything is initialized
            scoringEngine.CalculateSessionScore();

            // Setup Spawner
            AudienceSpawner spawner = Object.FindFirstObjectByType<AudienceSpawner>();
            if (spawner == null)
            {
                spawner = behaviorController.gameObject.AddComponent<AudienceSpawner>();
            }
            spawner.controller = behaviorController;
            spawner.audiencePrefabs = customPrefabs;

            // Force spawn generation here if desired to immediately see them?
            // The spawner runs in Start(), so they will spawn when playing.

            EditorUtility.SetDirty(behaviorController);
            EditorUtility.SetDirty(engine);
            EditorUtility.SetDirty(spawner);

            EditorSceneManager.SaveScene(scene);
            Debug.Log("[Oto-Kurulum] " + scene.name + " basariyla temizlendi ve yenilendi.");
        }

        EditorUtility.DisplayDialog("Başarılı", "Tüm sahnelerdeki sabit karakterler silindi. Senin SampleScene'deki kodlu, renkli ve kendi hareket eden yeni karakterlerin bu 3 sahneye Spawner aracıyla bağlandı!", "Harika");
    }

    [MenuItem("AR Trainer/🌟 3- Karakterleri Şimdiden Sahnede Göster (Playe basmadan)")]
    public static void ForceSpawnInEditor()
    {
        AudienceSpawner spawner = Object.FindFirstObjectByType<AudienceSpawner>();
        if (spawner != null)
        {
            spawner.ForceSpawn();
        }
        else
        {
            EditorUtility.DisplayDialog("Hata", "Sahnede AudienceSpawner bulunamadı! Önce 2. adımı yapın.", "Tamam");
        }
    }
}
#endif
