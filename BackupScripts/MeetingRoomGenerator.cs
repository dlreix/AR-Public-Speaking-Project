using UnityEngine;

/// <summary>
/// Meeting Room Scene Generator — Toplantı Odası
/// Sadece oda yapısı + detaylar. Mobilya asset olarak eklenecek.
///
/// İçerik: Duvarlar, zemin, tavan, pencereler, kapı, ışıklar,
///         projeksiyon perdesi, whiteboard, spawn point
///
/// KULLANIM:
/// 1. GameObject > Create Empty, "MeetingRoomGenerator" adını ver
/// 2. Bu scripti sürükle
/// 3. Inspector > sağ tık > "Generate Scene"
/// </summary>
[ExecuteInEditMode]
public class MeetingRoomGenerator : MonoBehaviour
{
    [Header("== GENERATE ==")]
    public bool generateNow = false;

    [Header("Oda Boyutları")]
    public float roomWidth  = 8f;
    public float roomDepth  = 6f;
    public float roomHeight = 3.2f;

    [Header("Textures (Inspector'dan sürükle)")]
    public Texture2D texFloor;
    public Texture2D texWall;
    public float textureTiling = 1f;

    private Material matFloor, matWall, matCeiling, matWood,
                     matWindow, matDoor, matBoard, matBoardFrame,
                     matScreen, matBeam;

    void OnValidate() { if (generateNow) { generateNow = false; Generate(); } }
    void Start()      { if (Application.isPlaying) Generate(); }

    [ContextMenu("Generate Scene")]
    public void Generate()
    {
        ClearOld();
        CreateMaterials();
        var root = new GameObject("MeetingRoom_Scene");
        BuildRoom(root);
        BuildWindows(root);
        BuildDoor(root);
        BuildWhiteboard(root);
        BuildProjectionScreen(root);
        BuildCeilingDetails(root);
        BuildLighting(root);
        PlaceSpawnPoint(root);
        Debug.Log("[MeetingRoomGenerator] Toplantı odası oluşturuldu!");
    }

    // ── Materials ─────────────────────────────────────────────
    void CreateMaterials()
    {
        matFloor      = Mat(new Color(0.75f, 0.68f, 0.58f), texFloor, textureTiling * 2f);
        matWall       = Mat(new Color(0.92f, 0.90f, 0.87f), texWall, textureTiling);
        matCeiling    = Mat(new Color(0.96f, 0.96f, 0.95f));
        matWood       = Mat(new Color(0.55f, 0.35f, 0.18f));
        matWindow     = MatGlass(new Color(0.80f, 0.88f, 0.95f), 0.25f);
        matDoor       = Mat(new Color(0.50f, 0.32f, 0.18f));
        matBoard      = Mat(new Color(0.97f, 0.97f, 0.97f));
        matBoardFrame = Mat(new Color(0.30f, 0.30f, 0.32f));
        matScreen     = Mat(new Color(0.98f, 0.98f, 0.98f));
        matBeam       = Mat(new Color(0.85f, 0.85f, 0.83f));
    }

    Material Mat(Color c, Texture2D tex = null, float tiling = 1f)
    {
        var m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        if (m.shader.name == "Hidden/InternalErrorShader")
            m = new Material(Shader.Find("Standard"));
        m.color = c;
        if (tex != null)
        {
            m.mainTexture = tex;
            m.mainTextureScale = new Vector2(tiling, tiling);
        }
        return m;
    }

    Material MatGlass(Color c, float alpha)
    {
        var m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        if (m.shader.name == "Hidden/InternalErrorShader")
            m = new Material(Shader.Find("Standard"));
        m.SetFloat("_Surface", 1);
        c.a = alpha;
        m.color = c;
        m.SetFloat("_Smoothness", 0.95f);
        m.renderQueue = 3000;
        m.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        m.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        m.SetInt("_ZWrite", 0);
        m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        return m;
    }

    void ClearOld()
    {
        var old = GameObject.Find("MeetingRoom_Scene");
        if (old != null) DestroyImmediate(old);
    }

    // ── Oda ───────────────────────────────────────────────────
    void BuildRoom(GameObject root)
    {
        var env = Child("Room", root);

        // Zemin
        Cube("Floor", env,
            V(0, -0.10f, roomDepth/2f),
            V(roomWidth, 0.2f, roomDepth), matFloor);

        // Duvarlar
        Cube("Wall_Front", env,
            V(0, roomHeight/2f, 0),
            V(roomWidth, roomHeight, 0.15f), matWall);

        Cube("Wall_Back", env,
            V(0, roomHeight/2f, roomDepth),
            V(roomWidth, roomHeight, 0.15f), matWall);

        Cube("Wall_Left", env,
            V(-roomWidth/2f, roomHeight/2f, roomDepth/2f),
            V(0.15f, roomHeight, roomDepth), matWall);

        Cube("Wall_Right", env,
            V(roomWidth/2f, roomHeight/2f, roomDepth/2f),
            V(0.15f, roomHeight, roomDepth), matWall);

        // Tavan
        Cube("Ceiling", env,
            V(0, roomHeight, roomDepth/2f),
            V(roomWidth, 0.15f, roomDepth), matCeiling);

        // Süpürgelik (tüm duvarlarda)
        float skH = 0.10f;
        Cube("Skirting_Front", env,
            V(0, skH/2f, 0.08f), V(roomWidth, skH, 0.02f), matWood);
        Cube("Skirting_Back", env,
            V(0, skH/2f, roomDepth-0.08f), V(roomWidth, skH, 0.02f), matWood);
        Cube("Skirting_Left", env,
            V(-roomWidth/2f+0.08f, skH/2f, roomDepth/2f), V(0.02f, skH, roomDepth), matWood);
        Cube("Skirting_Right", env,
            V(roomWidth/2f-0.08f, skH/2f, roomDepth/2f), V(0.02f, skH, roomDepth), matWood);
    }

    // ── Pencereler — sağ duvarda ──────────────────────────────
    void BuildWindows(GameObject root)
    {
        var w = Child("Windows", root);
        float wx = roomWidth / 2f - 0.06f;

        for (int i = 0; i < 2; i++)
        {
            float z = roomDepth * 0.33f + i * roomDepth * 0.34f;
            float wY = 1.7f;

            // Çerçeve
            Cube($"WinFrame_{i}", w,
                V(wx, wY, z), V(0.06f, 1.5f, 1.6f), matWood);

            // Cam
            Cube($"WinGlass_{i}", w,
                V(wx + 0.02f, wY, z), V(0.03f, 1.3f, 1.4f), matWindow);

            // Pervaz
            Cube($"WinSill_{i}", w,
                V(wx - 0.06f, wY - 0.75f - 0.03f, z), V(0.18f, 0.04f, 1.6f), matWood);
        }
    }

    // ── Kapı — sol duvarda ────────────────────────────────────
    void BuildDoor(GameObject root)
    {
        var d = Child("Door", root);
        float dx = -roomWidth / 2f + 0.12f;
        float dz = roomDepth - 1.2f;

        // Çerçeve
        Cube("DoorFrame", d,
            V(dx, 1.15f, dz), V(0.14f, 2.3f, 1.0f), matWood);

        // Kapı paneli
        Cube("DoorPanel", d,
            V(dx + 0.03f, 1.10f, dz), V(0.06f, 2.1f, 0.88f), matDoor);

        // Kapı kolu
        Cube("DoorHandle", d,
            V(dx + 0.06f, 1.0f, dz - 0.30f), V(0.03f, 0.04f, 0.12f), matBoardFrame);
    }

    // ── Beyaz Tahta — ön duvarda ──────────────────────────────
    void BuildWhiteboard(GameObject root)
    {
        var t = Child("Whiteboard", root);

        float boardW = 2.5f;
        float boardH = 1.3f;
        float boardY = 1.5f;
        float boardX = -1.5f;

        Cube("WB_Surface", t,
            V(boardX, boardY, 0.12f), V(boardW, boardH, 0.03f), matBoard);
        Cube("WB_Frame", t,
            V(boardX, boardY, 0.10f), V(boardW + 0.08f, boardH + 0.08f, 0.02f), matBoardFrame);

        // Kalem rafı
        Cube("WB_Tray", t,
            V(boardX, boardY - boardH/2f - 0.04f, 0.15f), V(boardW * 0.6f, 0.04f, 0.08f), matBoardFrame);
    }

    // ── Projeksiyon Perdesi — ön duvarda ──────────────────────
    void BuildProjectionScreen(GameObject root)
    {
        var s = Child("ProjectionScreen", root);

        float screenX = 1.5f;
        float screenY = 1.6f;

        Cube("Screen", s,
            V(screenX, screenY, 0.12f), V(2.2f, 1.5f, 0.03f), matScreen);
        Cube("ScreenFrame", s,
            V(screenX, screenY, 0.10f), V(2.3f, 1.6f, 0.02f), matBoardFrame);

        // Üst kasa
        Cube("ScreenBox", s,
            V(screenX, screenY + 0.80f, 0.12f), V(2.4f, 0.12f, 0.12f), matBoardFrame);
    }

    // ── Tavan Detayları ───────────────────────────────────────
    void BuildCeilingDetails(GameObject root)
    {
        var c = Child("CeilingDetails", root);

        // Tavan çıtaları — ince dekoratif çizgiler
        for (int i = 0; i < 3; i++)
        {
            float z = 1.5f + i * 1.5f;
            Cube($"CeilingStrip_{i}", c,
                V(0, roomHeight - 0.02f, z),
                V(roomWidth - 0.5f, 0.03f, 0.08f), matBeam);
        }
    }

    // ── Işıklandırma ──────────────────────────────────────────
    void BuildLighting(GameObject root)
    {
        var lr = Child("Lighting", root);

        // 2x2 tavan ışığı
        float[] xPos = { -1.8f, 1.8f };
        float[] zPos = { 2f, 4f };

        int idx = 0;
        foreach (float x in xPos)
        {
            foreach (float z in zPos)
            {
                AddLight($"Light_{idx}", lr,
                    V(x, roomHeight - 0.2f, z),
                    LightType.Spot, 5f, 10f,
                    new Color(1f, 0.95f, 0.88f));

                // Gömme ışık kasası
                Cube($"Housing_{idx}", lr,
                    V(x, roomHeight - 0.08f, z),
                    V(0.4f, 0.04f, 0.4f), matCeiling);
                idx++;
            }
        }
    }

    // ── Spawn Point ───────────────────────────────────────────
    void PlaceSpawnPoint(GameObject root)
    {
        var sp = new GameObject("SpawnPoint");
        sp.transform.parent = root.transform;
        sp.transform.position = V(0, 0.1f, 1.0f);  // masanın bir ucunda
        sp.transform.rotation = Quaternion.Euler(0, 180, 0);
    }

    // ── Helpers ───────────────────────────────────────────────
    GameObject Cube(string n, GameObject p, Vector3 pos, Vector3 scale, Material mat)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = n;
        go.transform.parent = p.transform;
        go.transform.position = pos;
        go.transform.localScale = scale;
        go.GetComponent<Renderer>().material = mat;
        return go;
    }

    GameObject Child(string n, GameObject p)
    {
        var go = new GameObject(n);
        go.transform.parent = p.transform;
        return go;
    }

    Vector3 V(float x, float y, float z) => new Vector3(x, y, z);

    void AddLight(string name, GameObject parent, Vector3 pos,
                  LightType type, float intensity, float range, Color color)
    {
        var go = new GameObject(name);
        go.transform.parent   = parent.transform;
        go.transform.position = pos;
        if (type == LightType.Spot) go.transform.rotation = Quaternion.Euler(90, 0, 0);
        var l       = go.AddComponent<Light>();
        l.type      = type;
        l.intensity = intensity;
        l.range     = range;
        l.color     = color;
        if (type == LightType.Spot) l.spotAngle = 100f;
        l.shadows   = LightShadows.Soft;
    }
}
