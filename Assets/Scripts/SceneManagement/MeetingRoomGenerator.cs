using UnityEngine;

namespace VRSpeakingTrainer.SceneManagement
{
/// <summary>
/// Meeting Room Scene Generator â€” ToplantÄ± OdasÄ±
/// Sadece oda yapÄ±sÄ± + detaylar. Mobilya asset olarak eklenecek.
///
/// Ä°Ã§erik: Duvarlar, zemin, tavan, pencereler, kapÄ±, Ä±ÅŸÄ±klar,
///         projeksiyon perdesi, whiteboard, spawn point
///
/// KULLANIM:
/// 1. GameObject > Create Empty, "MeetingRoomGenerator" adÄ±nÄ± ver
/// 2. Bu scripti sÃ¼rÃ¼kle
/// 3. Inspector > saÄŸ tÄ±k > "Generate Scene"
/// </summary>
[ExecuteInEditMode]
public class MeetingRoomGenerator : MonoBehaviour
{
    [Header("== GENERATE ==")]
    public bool generateNow = false;

    [Header("Oda BoyutlarÄ±")]
    public float roomWidth  = 8f;
    public float roomDepth  = 6f;
    public float roomHeight = 3.2f;

    [Header("Textures (Inspector'dan sÃ¼rÃ¼kle)")]
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
        // Zorunlu oda boyutlari (Dikdortgen masa ve projeksiyon duzeni icin)
        roomWidth = 6f;
        roomDepth = 8.5f;
        
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
        BuildMeetingTable(root);
        PlaceSpawnPoint(root);
        Debug.Log("[MeetingRoomGenerator] ToplantÄ± odasÄ± oluÅŸturuldu!");
    }

    // â”€â”€ Materials â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
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

    // â”€â”€ Oda â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
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

        // SÃ¼pÃ¼rgelik (tÃ¼m duvarlarda)
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

    // â”€â”€ Pencereler â€” saÄŸ duvarda â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
    void BuildWindows(GameObject root)
    {
        var w = Child("Windows", root);
        float wx = roomWidth / 2f - 0.06f;

        for (int i = 0; i < 2; i++)
        {
            float z = roomDepth * 0.33f + i * roomDepth * 0.34f;
            float wY = 1.7f;

            // Ã‡erÃ§eve
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

    // â”€â”€ KapÄ± â€” sol duvarda â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
    void BuildDoor(GameObject root)
    {
        var d = Child("Door", root);
        float dx = -roomWidth / 2f + 0.12f;
        float dz = roomDepth - 1.2f;

        // Ã‡erÃ§eve
        Cube("DoorFrame", d,
            V(dx, 1.15f, dz), V(0.14f, 2.3f, 1.0f), matWood);

        // KapÄ± paneli
        Cube("DoorPanel", d,
            V(dx + 0.03f, 1.10f, dz), V(0.06f, 2.1f, 0.88f), matDoor);

        // KapÄ± kolu
        Cube("DoorHandle", d,
            V(dx + 0.06f, 1.0f, dz - 0.30f), V(0.03f, 0.04f, 0.12f), matBoardFrame);
    }

    // â”€â”€ Beyaz Tahta â€” Ã¶n duvarda â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
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

        // Kalem rafÄ±
        Cube("WB_Tray", t,
            V(boardX, boardY - boardH/2f - 0.04f, 0.15f), V(boardW * 0.6f, 0.04f, 0.08f), matBoardFrame);
    }

    // â”€â”€ Projeksiyon Perdesi â€” Ã¶n duvarda â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
    void BuildProjectionScreen(GameObject root)
    {
        var s = Child("ProjectionScreen", root);

        float screenX = 1.5f;
        float screenY = 1.6f;

        Cube("Screen", s,
            V(screenX, screenY, 0.12f), V(2.2f, 1.5f, 0.03f), matScreen);
        Cube("ScreenFrame", s,
            V(screenX, screenY, 0.10f), V(2.3f, 1.6f, 0.02f), matBoardFrame);

        // Ãœst kasa
        Cube("ScreenBox", s,
            V(screenX, screenY + 0.80f, 0.12f), V(2.4f, 0.12f, 0.12f), matBoardFrame);
    }

    // â”€â”€ Tavan DetaylarÄ± â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
    void BuildCeilingDetails(GameObject root)
    {
        var c = Child("CeilingDetails", root);

        // Tavan Ã§Ä±talarÄ± â€” ince dekoratif Ã§izgiler
        for (int i = 0; i < 3; i++)
        {
            float z = 1.5f + i * 1.5f;
            Cube($"CeilingStrip_{i}", c,
                V(0, roomHeight - 0.02f, z),
                V(roomWidth - 0.5f, 0.03f, 0.08f), matBeam);
        }
    }

    // â”€â”€ IÅŸÄ±klandÄ±rma â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
    void BuildLighting(GameObject root)
    {
        var lr = Child("Lighting", root);

        // 2x2 tavan Ä±ÅŸÄ±ÄŸÄ±
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

                // GÃ¶mme Ä±ÅŸÄ±k kasasÄ±
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
        sp.transform.position = V(0, 0.1f, 1.0f);  // masanin bir ucunda
        sp.transform.rotation = Quaternion.Euler(0, 180, 0);
    }

    // ── Toplanti Masasi ve Sandalyeler ────────────────────────────────
    void BuildMeetingTable(GameObject root)
    {
        var t = Child("MeetingTableGroup", root);

        // Masa Konumu (Odada uzunlamasina)
        Vector3 tableCenter = V(0, 0, 4.5f);

        // Masa Ustu (Dikdortgen)
        var tableTop = GameObject.CreatePrimitive(PrimitiveType.Cube);
        tableTop.name = "TableTop";
        tableTop.transform.parent = t.transform;
        tableTop.transform.position = tableCenter + V(0, 0.85f, 0);
        tableTop.transform.localScale = V(1.6f, 0.05f, 4.5f);
        tableTop.GetComponent<Renderer>().material = matWood;

        // Masa Ayagi
        var tableLeg = GameObject.CreatePrimitive(PrimitiveType.Cube);
        tableLeg.name = "TableLeg";
        tableLeg.transform.parent = t.transform;
        tableLeg.transform.position = tableCenter + V(0, 0.425f, 0);
        tableLeg.transform.localScale = V(0.6f, 0.85f, 3.5f); 
        tableLeg.GetComponent<Renderer>().material = matBoardFrame;

        // Sandalyeler
        float chairY = 0.5f; // Sandalye oturak yuksekligi
        int chairIdx = 0;

        // Sandalye Olusturma Fonksiyonu
        System.Action<Vector3, Vector3> PlaceChair = (pos, lookTarget) =>
        {
            var chairGroup = Child($"MeetingChairGroup_{chairIdx}", t);
            chairGroup.transform.position = pos;
            
            // Sandalyenin Z ekseni hedefe bakacak
            chairGroup.transform.LookAt(lookTarget);

            // Sandalye Oturagi (Bunun bounds'u AudienceSpawner tarafindan bulunacak)
            var seatBase = Cube("MeetingSeat", chairGroup, pos + V(0, chairY, 0), V(0.5f, 0.05f, 0.5f), matDoor);
            seatBase.transform.localRotation = Quaternion.identity;

            // Sandalye Ayagi
            var seatLegObj = Cube("ChairLeg", chairGroup, pos + V(0, chairY / 2f, 0), V(0.08f, chairY, 0.08f), matBoardFrame);
            seatLegObj.transform.localRotation = Quaternion.identity;

            // Sandalye Sirtligi (Sirtlik arkada)
            Vector3 backrestPos = pos - chairGroup.transform.forward * 0.23f + V(0, chairY + 0.25f, 0);
            var backrest = Cube("ChairBack", chairGroup, backrestPos, V(0.5f, 0.5f, 0.05f), matDoor);
            backrest.transform.localRotation = Quaternion.identity;

            chairIdx++;
        };

        // Sol taraftaki sandalyeler (x = -1.2f, z = 2.7, 3.8, 4.9, 6.0)
        float[] zPositions = { 2.7f, 3.8f, 4.9f, 6.0f };
        foreach (float z in zPositions)
        {
            PlaceChair(V(-1.2f, 0, z), V(0, 0, z)); // Masaya dogru bak
        }

        // Sag taraftaki sandalyeler (x = 1.2f)
        foreach (float z in zPositions)
        {
            PlaceChair(V(1.2f, 0, z), V(0, 0, z)); // Masaya dogru bak
        }

        // Patron Sandalyesi (En ucta)
        PlaceChair(V(0, 0, 7.1f), V(0, 0, 4.5f)); // Oyuncuya (masaya) dogru bak
    }

    // â”€â”€ Helpers â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
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

}
