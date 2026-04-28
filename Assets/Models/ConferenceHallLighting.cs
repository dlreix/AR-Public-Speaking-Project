using UnityEngine;

/// <summary>
/// Konferans salonu için ışıklandırma scripti.
/// Referans fotoğraflardaki gömme tavan ışıklarını oluşturur.
///
/// KULLANIM:
/// 1. GameObject > Create Empty, "LightingController" adını ver
/// 2. Bu scripti sürükle
/// 3. Inspector'da ayarları yap
/// 4. Sağ tık > "Generate Lighting"
/// </summary>
[ExecuteInEditMode]
public class ConferenceHallLighting : MonoBehaviour
{
    [Header("== GENERATE ==")]
    public bool generateNow = false;

    [Header("Oda Boyutları")]
    public float roomWidth  = 20f;
    public float roomDepth  = 22f;
    public float roomHeight = 6f;

    [Header("Tavan Gömme Işıkları")]
    [Tooltip("Kirişler arası konumlarda kare gömme ışıklar")]
    public int   lightsPerRow    = 3;
    public float intensity       = 6f;
    public float range           = 18f;
    public float spotAngle       = 100f;
    public Color lightColor      = new Color(1f, 0.93f, 0.82f);
    public float housingSize     = 0.55f;

    [Header("Sahne Aydınlatma")]
    public float stageSpotIntensity = 6f;

    [Header("Genel Ortam")]
    public float directionalIntensity = 0.3f;

    void OnValidate()
    {
        if (generateNow)
        {
            generateNow = false;
            GenerateLighting();
        }
    }

    [ContextMenu("Generate Lighting")]
    public void GenerateLighting()
    {
        var old = GameObject.Find("LightingSystem");
        if (old != null) DestroyImmediate(old);

        var root = new GameObject("LightingSystem");

        BuildCeilingLights(root);
        BuildStageSpots(root);
        BuildAmbientLight(root);

        Debug.Log("[Lighting] Işıklandırma oluşturuldu!");
    }

    // ── Referans fotoğraftaki gibi gömme tavan ışıkları ──
    // Kirişler X = {-6, 0, 6} ve Z her 5m'de
    // Işıklar kirişler ARASINDA konumlandırılır
    void BuildCeilingLights(GameObject root)
    {
        var parent = new GameObject("CeilingLights");
        parent.transform.parent = root.transform;

        // Kirişler arası X pozisyonları (referansta 3 kiriş var: -6, 0, 6)
        // Işıklar bu kirişlerin arasına yerleşir
        float[] xZones = { -8f, -3f, 3f, 8f };

        // Z pozisyonları — tavan panelleri arasında (kirişler Z her ~5m)
        // Referansta 4 satır gömme ışık görünüyor
        float[] zPositions = { 4f, 9f, 14f, 19f };

        int count = 0;
        foreach (float z in zPositions)
        {
            // Her Z satırında lightsPerRow kadar ışık, xZones'dan seç
            int perRow = Mathf.Min(lightsPerRow, xZones.Length);
            float totalSpan = xZones[xZones.Length - 1] - xZones[0];

            for (int i = 0; i < perRow; i++)
            {
                float x;
                if (perRow == 1)
                    x = 0f;
                else if (perRow <= xZones.Length)
                    x = Mathf.Lerp(xZones[0], xZones[xZones.Length - 1], (float)i / (perRow - 1));
                else
                    x = xZones[i % xZones.Length];

                CreateRecessedLight(parent, $"CL_{count}", x, z);
                count++;
            }
        }
    }

    void CreateRecessedLight(GameObject parent, string name, float x, float z)
    {
        float y = roomHeight - 0.35f;

        // ── Spot Light ──
        var lightGO = new GameObject(name);
        lightGO.transform.parent = parent.transform;
        lightGO.transform.position = new Vector3(x, y, z);
        lightGO.transform.rotation = Quaternion.Euler(90, 0, 0);

        var light = lightGO.AddComponent<Light>();
        light.type = LightType.Spot;
        light.intensity = intensity;
        light.range = range;
        light.spotAngle = spotAngle;
        light.color = lightColor;
        light.shadows = LightShadows.Soft;

        // ── Gömme ışık kasası (kare beyaz panel) ──
        var housing = GameObject.CreatePrimitive(PrimitiveType.Cube);
        housing.name = $"Housing_{name}";
        housing.transform.parent = parent.transform;
        housing.transform.position = new Vector3(x, roomHeight - 0.16f, z);
        housing.transform.localScale = new Vector3(housingSize, 0.06f, housingSize);

        // Emit eden materyal — referanstaki gibi parlayan kare
        var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        if (mat.shader.name == "Hidden/InternalErrorShader")
            mat = new Material(Shader.Find("Standard"));
        mat.color = new Color(0.98f, 0.95f, 0.90f);
        mat.EnableKeyword("_EMISSION");
        mat.SetColor("_EmissionColor", lightColor * 1.5f);
        housing.GetComponent<Renderer>().material = mat;

        // Collider kaldır (gereksiz)
        DestroyImmediate(housing.GetComponent<Collider>());
    }

    // ── Sahne spot ışıkları (referanstaki projektör benzeri) ──
    void BuildStageSpots(GameObject root)
    {
        var parent = new GameObject("StageSpots");
        parent.transform.parent = root.transform;

        float y = roomHeight - 0.5f;

        // Referansta sahne üzerinde 2-3 spot/projektör var
        float[][] spots = {
            new float[] { -3f, 2f, 50f },   // sol spot
            new float[] {  0f, 2f, 40f },    // orta spot (projeksiyon)
            new float[] {  3f, 2f, 50f },    // sağ spot
        };

        for (int i = 0; i < spots.Length; i++)
        {
            var go = new GameObject($"StageSpot_{i}");
            go.transform.parent = parent.transform;
            go.transform.position = new Vector3(spots[i][0], y, spots[i][1]);
            go.transform.rotation = Quaternion.Euler(spots[i][2], 0, 0);

            var light = go.AddComponent<Light>();
            light.type = LightType.Spot;
            light.intensity = stageSpotIntensity;
            light.range = 14f;
            light.spotAngle = 70f;
            light.color = new Color(1f, 0.96f, 0.90f);
            light.shadows = LightShadows.Soft;
        }
    }

    // ── Genel ortam aydınlatma ──
    void BuildAmbientLight(GameObject root)
    {
        var go = new GameObject("AmbientFill");
        go.transform.parent = root.transform;
        go.transform.rotation = Quaternion.Euler(50, -30, 0);

        var light = go.AddComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = directionalIntensity;
        light.color = new Color(1f, 0.95f, 0.90f);
        light.shadows = LightShadows.Soft;
    }
}
