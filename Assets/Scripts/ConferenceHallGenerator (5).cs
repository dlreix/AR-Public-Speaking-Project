using UnityEngine;

/// <summary>
/// AR Public Speaking Trainer - Conference Hall Scene Generator
/// - Full width seating (duvardan duvara)
/// - 3 kolon: [Sol] | koridor | [Orta] | koridor | [Sag]
/// - Hafif kavisli siralar (her sira segmentlere bolunup egrilir)
///
/// HOW TO USE:
/// 1. GameObject > Create Empty, name it "Generator"
/// 2. Drag this script onto it
/// 3. Inspector > right-click script component > "Generate Scene"
/// </summary>
[ExecuteInEditMode]
public class ConferenceHallGenerator : MonoBehaviour
{
    [Header("== GENERATE ==")]
    public bool generateNow = false;

    [Header("Room Dimensions")]
    public float roomWidth  = 20f;
    public float roomDepth  = 22f;
    public float roomHeight = 6f;

    [Header("Seating")]
    public int   rowCount      = 9;
    public float rowSpacing    = 1.3f;
    public float rowDepth      = 1.1f;
    public float tierHeight    = 0.35f;

    [Header("Curve Settings")]
    public float curveRadius   = 80f;  // kavsin yari capi — buyuk = az kavs, kucuk = cok kavs
    public int   curveSegments = 25;   // her sira kac segmente bolunur (basamak zemini)

    [Header("3-Column Split")]
    public float corridorWidth    = 1.3f;
    public float leftBlockRatio   = 0.27f;
    public float centerBlockRatio = 0.46f;

    [Header("Stage")]
    public float stageHeight = 0.45f;
    public float stageDepth  = 5f;

    [Header("Textures (Inspector'dan sürükle)")]
    public Texture2D texFloor;
    public Texture2D texWall;
    public Texture2D texWood;
    public Texture2D texDesk;
    public float textureTiling = 1f;

    private Material matFloor, matWall, matCeiling, matWood,
                     matAcoustic, matStage, matScreen,
                     matDesk, matBeam, matColumn, matWhiteboard, matCurtain;

    void OnValidate() { if (generateNow) { generateNow = false; Generate(); } }
    void Start()      { /* Scene geometry is serialized; do not rebuild during VR runtime. */ }

    [ContextMenu("Generate Scene")]
    public void Generate()
    {
        CreateMaterials();
        ClearOld();
        var root = new GameObject("ConferenceHall_Scene");
        BuildRoom(root);
        BuildCeilingBeams(root);
        BuildStage(root);
        BuildTieredRows(root);
        BuildCenterColumn(root);
        BuildDoors(root);
        BuildLighting(root);
        PlaceSpawnPoint(root);
        Debug.Log("[ConferenceHallGenerator] Scene generated!");
    }

    // ── Materials ─────────────────────────────────────────────
    void CreateMaterials()
    {
        matFloor      = Mat(new Color(0.78f, 0.76f, 0.72f), texFloor, textureTiling * 2f);
        matWall       = Mat(new Color(0.82f, 0.62f, 0.42f), texWall, textureTiling);
        matCeiling    = Mat(new Color(0.95f, 0.92f, 0.88f));
        matWood       = Mat(new Color(0.72f, 0.45f, 0.22f), texWood, textureTiling);
        matAcoustic   = Mat(new Color(0.65f, 0.22f, 0.15f));
        matStage      = Mat(new Color(0.76f, 0.74f, 0.70f), texFloor, textureTiling * 2f);
        matScreen     = Mat(new Color(0.97f, 0.97f, 0.97f));
        matDesk       = Mat(new Color(0.72f, 0.45f, 0.22f), texDesk, textureTiling);
        matBeam       = Mat(new Color(0.12f, 0.12f, 0.12f));
        matColumn     = Mat(new Color(0.18f, 0.18f, 0.22f));
        matWhiteboard = Mat(new Color(0.98f, 0.98f, 0.98f));
        matCurtain    = Mat(new Color(0.45f, 0.08f, 0.08f));
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

    void ClearOld()
    {
        var old = GameObject.Find("ConferenceHall_Scene");
        if (old != null) DestroyImmediate(old);
    }

    // ── Room ──────────────────────────────────────────────────
    void BuildRoom(GameObject root)
    {
        var env = Child("Environment", root);
        Cube("Floor",      env, V(0, 0, roomDepth/2f),                         V(roomWidth, 0.1f, roomDepth),  matFloor);
        Cube("Wall_Back",  env, V(0, roomHeight/2f, roomDepth),                V(roomWidth, roomHeight, 0.3f), matWall);
        Cube("Wall_Front", env, V(0, roomHeight/2f, 0),                        V(roomWidth, roomHeight, 0.3f), matCeiling);
        Cube("Wall_Left",  env, V(-roomWidth/2f, roomHeight/2f, roomDepth/2f), V(0.3f, roomHeight, roomDepth), matWall);
        Cube("Wall_Right", env, V( roomWidth/2f, roomHeight/2f, roomDepth/2f), V(0.3f, roomHeight, roomDepth), matWall);
        Cube("Ceiling",    env, V(0, roomHeight, roomDepth/2f),                V(roomWidth, 0.3f, roomDepth),  matCeiling);

        float[] pz = { 3f, 11f, 16f };
        for (int i = 0; i < 3; i++)
        {
            Cube($"AcPanel_L{i}", env, V(-roomWidth/2f+0.2f, 2.8f, pz[i]), V(0.15f, 2.2f, 2.8f), matAcoustic);
            Cube($"AcPanel_R{i}", env, V( roomWidth/2f-0.2f, 2.8f, pz[i]), V(0.15f, 2.2f, 2.8f), matAcoustic);
        }
    }

    // ── Ceiling beams ─────────────────────────────────────────
    void BuildCeilingBeams(GameObject root)
    {
        var b = Child("CeilingBeams", root);
        float y = roomHeight - 0.25f;
        foreach (float x in new float[] { -6f, 0f, 6f })
            Cube($"BeamLong_{x}", b, V(x, y, roomDepth/2f), V(0.4f, 0.5f, roomDepth), matBeam);
        for (int i = 0; i < 4; i++)
            Cube($"BeamCross_{i}", b, V(0, y, 4f+i*5f), V(roomWidth, 0.5f, 0.4f), matBeam);
    }

    // ── Stage ─────────────────────────────────────────────────
    void BuildStage(GameObject root)
    {
        var s = Child("Stage", root);
        Cube("Stage_Platform",   s, V(0, stageHeight/2f, stageDepth/2f),  V(roomWidth, stageHeight, stageDepth),  matStage);
        Cube("Stage_BackWall",   s, V(0, roomHeight/2f, 0.4f),            V(roomWidth, roomHeight, 0.2f),         Mat(new Color(0.15f, 0.15f, 0.18f)));
        Cube("ProjectionScreen", s, V(0, 3.2f, 0.56f),                   V(7f, 4.2f, 0.08f),                        matScreen);
        Cube("Desk_Surface",     s, V(4.5f, stageHeight+0.42f, 2.5f),    V(2f, 0.08f, 1f),                          matDesk);
        Cube("Desk_Body",        s, V(4.5f, stageHeight+0.18f, 2.5f),    V(2f, 0.36f, 1f),                          matWood);
        Cube("Monitor",          s, V(4.7f, stageHeight+0.88f, 2.2f),    V(0.6f, 0.4f, 0.05f),                      matBeam);
        Cube("MonitorStand",     s, V(4.7f, stageHeight+0.58f, 2.2f),    V(0.05f, 0.3f, 0.05f),                     matBeam);
        Cube("WB_Surface",       s, V(-4f, stageHeight+1.3f, 2f),        V(2.5f, 1.6f, 0.05f),                      matWhiteboard);
        Cube("WB_Frame",         s, V(-4f, stageHeight+1.3f, 2f),        V(2.6f, 1.7f, 0.04f),                      matBeam);
        Cube("WB_LegL",          s, V(-5.1f, stageHeight+0.3f, 2f),      V(0.05f, 0.8f, 0.05f),                     matBeam);
        Cube("WB_LegR",          s, V(-2.9f, stageHeight+0.3f, 2f),      V(0.05f, 0.8f, 0.05f),                     matBeam);
        Cube("Podium",           s, V(2.5f, stageHeight+0.5f, 3.5f),     V(0.6f, 1.0f, 0.5f),                       matWood);
    }

    // ── Tiered rows — curved, full width, 3-column ────────────
    void BuildTieredRows(GameObject root)
    {
        var rowRoot = Child("TieredRows", root);

        float usableW   = roomWidth - 0.2f;  // duvardan duvara — minimal boşluk
        float seatingW  = usableW - corridorWidth * 2f;
        float leftW     = seatingW * leftBlockRatio;
        float centerW   = seatingW * centerBlockRatio;
        float rightW    = seatingW * (1f - leftBlockRatio - centerBlockRatio);

        float originX   = -usableW / 2f;
        float leftCX    = originX + leftW / 2f;
        float centerCX  = originX + leftW + corridorWidth + centerW / 2f;
        float rightCX   = originX + leftW + corridorWidth + centerW + corridorWidth + rightW / 2f;

        float startZ = stageDepth + 3.0f;

        for (int row = 0; row < rowCount; row++)
        {
            float baseZ = startZ + row * rowSpacing;
            float y     = row * tierHeight;

            var rowGO = Child($"Row_{row+1}", rowRoot);

            // ── Basamak zemini — masanın arkasından başlar, bir sonraki sıraya kadar ──
            Cube($"Tier_{row}", rowGO,
                V(0, y, baseZ + rowSpacing / 2f + 0.10f),
                V(usableW, tierHeight, rowSpacing - 0.10f), matFloor);

            // ── Sol blok — kavisli tezgah ──
            BuildCurvedBlock(rowGO, $"L{row}", leftCX,   leftW,   y, baseZ, "L", row, curveRadius);
            // ── Orta blok ──
            BuildCurvedBlock(rowGO, $"C{row}", centerCX, centerW, y, baseZ, "C", row, curveRadius);
            // ── Sağ blok ──
            BuildCurvedBlock(rowGO, $"R{row}", rightCX,  rightW,  y, baseZ, "R", row, curveRadius);
        }

            // ── Arka platform — son sıranın arkasında düz alan ──
            float lastRowZ = startZ + (rowCount - 1) * rowSpacing;
            float lastY    = (rowCount - 1) * tierHeight;
            float platZ    = lastRowZ + rowSpacing;
            float platDepth = roomDepth - platZ;
            Cube("BackPlatform", rowRoot,
                V(0, lastY, platZ + platDepth / 2f),
                V(usableW, tierHeight, platDepth), matFloor);

            // ── Arka sırtlıklar — koridorlarda kesilmiş, 3 parça ──
            float brH = 0.65f;
            float backrestY = lastY + tierHeight / 2f + brH / 2f;
            float brZ = platZ;
            Cube("Backrest_L", rowRoot,
                V(leftCX, backrestY, brZ), V(leftW + 0.18f, brH, 0.06f), matWood);
            Cube("Backrest_C", rowRoot,
                V(centerCX, backrestY, brZ), V(centerW + 0.18f, brH, 0.06f), matWood);
            Cube("Backrest_R", rowRoot,
                V(rightCX, backrestY, brZ), V(rightW + 0.18f, brH, 0.06f), matWood);
    }

    void BuildCurvedBlock(GameObject parent, string name,
                          float cx, float blockW,
                          float y, float baseZ,
                          string prefix, int row, float radius)
    {
        float deskY     = y + tierHeight + 0.42f;
        float bodyH     = deskY - y;
        float bodyY     = y + bodyH / 2f;
        float bodyDepth = 0.50f;
        float bodyCZ    = baseZ + bodyDepth / 2f;

        // ── 1) Gövde — gri solid blok ──
        Cube($"Body_{name}", parent,
            V(cx, bodyY, bodyCZ), V(blockW, bodyH, bodyDepth), matStage);

        // ── 2) Tezgah kapağı — yan kapaklarla çakışacak kadar geniş ──
        Cube($"Desk_{name}", parent,
            V(cx, deskY, bodyCZ), V(blockW + 0.18f, 0.08f, bodyDepth + 0.08f), matDesk);

        // ── 3) Yan kapaklar — tamamen dışarıda, tezgah kenarıyla üstten çakışır ──
        float halfW = blockW / 2f;
        Cube($"SideL_{name}", parent,
            V(cx - halfW - 0.04f, bodyY, bodyCZ),
            V(0.08f, bodyH, bodyDepth), matWood);

        Cube($"SideR_{name}", parent,
            V(cx + halfW + 0.04f, bodyY, bodyCZ),
            V(0.08f, bodyH, bodyDepth), matWood);
        // ── 4) Oturma tablası — masanın arkasında, basamak üstünde ──
        float benchY      = y + tierHeight / 2f + 0.40f;
        float benchDepthZ = 0.42f;
        float benchZ      = baseZ + rowSpacing - benchDepthZ / 2f;
        float benchH      = benchY - (y - tierHeight / 2f);
        float benchCY     = (y - tierHeight / 2f) + benchH / 2f;

        Cube($"Bench_{name}", parent,
            V(cx, benchY, benchZ), V(blockW + 0.18f, 0.05f, benchDepthZ), matWood);

        // Oturma yan kapakları
        Cube($"BenchSideL_{name}", parent,
            V(cx - halfW - 0.04f, benchCY, benchZ),
            V(0.08f, benchH, benchDepthZ), matWood);

        Cube($"BenchSideR_{name}", parent,
            V(cx + halfW + 0.04f, benchCY, benchZ),
            V(0.08f, benchH, benchDepthZ), matWood);
    }

    // ── Column ────────────────────────────────────────────────
    void BuildCenterColumn(GameObject root)
    {
        Cube("Column_Right", root,
            V(7f, roomHeight/2f, roomDepth * 0.45f),
            V(0.6f, roomHeight, 0.6f), matColumn);
    }

    // ── Doors — çift kapı tasarımı ──────────────────────────
    void BuildDoors(GameObject root)
    {
        var d  = Child("Doors", root);
        float wx = -roomWidth/2f + 0.2f;

        // Çift kanatlı kapı — koltuklara yakın
        float dz = 7f;
        Cube("DoorFrame",  d, V(wx, 1.5f, dz),            V(0.18f, 3.0f, 2.0f), matWood);
        Cube("DoorLeafL",  d, V(wx+0.06f, 1.4f, dz-0.45f), V(0.1f, 2.6f, 0.85f), matCurtain);
        Cube("DoorLeafR",  d, V(wx+0.06f, 1.4f, dz+0.45f), V(0.1f, 2.6f, 0.85f), matCurtain);
    }

    // ── Lighting ──────────────────────────────────────────────
    void BuildLighting(GameObject root)
    {
        var lr = Child("Lighting", root);
        AddLight("Light_Main",   lr, V(0, roomHeight-0.5f, roomDepth/2f), LightType.Point, 2.5f, 25f, new Color(1f, 0.92f, 0.80f));
        AddLight("Spot_Stage_L", lr, V(-3f, roomHeight-0.5f, 2f),         LightType.Spot,  3f,   15f, new Color(1f, 0.95f, 0.85f));
        AddLight("Spot_Stage_R", lr, V( 3f, roomHeight-0.5f, 2f),         LightType.Spot,  3f,   15f, new Color(1f, 0.95f, 0.85f));
        for (int i = 0; i < 3; i++)
            AddLight($"Light_Aud_{i}", lr,
                V(0, roomHeight-0.5f, stageDepth+4f+i*5f),
                LightType.Point, 1.8f, 20f, new Color(1f, 0.90f, 0.78f));
    }

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
        if (type == LightType.Spot) l.spotAngle = 45f;
    }

    // ── Spawn point ───────────────────────────────────────────
    void PlaceSpawnPoint(GameObject root)
    {
        var sp = new GameObject("PlayerSpawnPoint");
        sp.transform.parent   = root.transform;
        sp.transform.position = new Vector3(0, stageHeight, 3f);
        sp.transform.rotation = Quaternion.Euler(0, 180, 0);
        Debug.Log($"[SpawnPoint] {sp.transform.position}");
    }

    // ── Helpers ───────────────────────────────────────────────
    GameObject Child(string name, GameObject parent)
    {
        var go = new GameObject(name);
        go.transform.parent = parent.transform;
        return go;
    }

    GameObject Cube(string name, GameObject parent, Vector3 pos, Vector3 scale, Material mat)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name                 = name;
        go.transform.parent     = parent.transform;
        go.transform.position   = pos;
        go.transform.localScale = scale;
        if (mat != null) go.GetComponent<Renderer>().material = mat;
        return go;
    }

    Vector3 V(float x, float y, float z) => new Vector3(x, y, z);
}
