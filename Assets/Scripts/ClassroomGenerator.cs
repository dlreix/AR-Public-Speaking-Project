using UnityEngine;

/// <summary>
/// Classroom scene generator.
/// Generates only the structural classroom content owned by the generator:
/// - room shell
/// - lighting
/// - whiteboard
/// - student rows
/// - spawn point
/// Manual scene assets such as teacher desk, chair, computer, door, and windows
/// should stay outside the generated root.
/// </summary>
[ExecuteInEditMode]
public class ClassroomGenerator : MonoBehaviour
{
    [Header("== GENERATE ==")]
    public bool generateNow = false;

    [Header("Room Dimensions")]
    public float roomWidth = 10f;
    public float roomDepth = 9.8f;
    public float roomHeight = 3.5f;

    [Header("Student Row Settings")]
    public int rowCount = 5;
    public float rowSpacing = 1.2f;

    [Header("3-Column Layout")]
    public float corridorWidth = 1.0f;
    public float leftBlockRatio = 0.27f;
    public float centerBlockRatio = 0.46f;

    [Header("Front Teaching Area")]
    public float teacherAreaDepth = 2.5f;

    [Header("Textures")]
    public Texture2D texFloor;
    public Texture2D texWall;
    public Texture2D texWood;
    public float textureTiling = 1f;

    private Material matFloor;
    private Material matWall;
    private Material matCeiling;
    private Material matWood;
    private Material matDesk;
    private Material matChair;
    private Material matBoard;
    private Material matBoardFrame;
    private Material matGray;

    void OnValidate()
    {
        if (generateNow)
        {
            generateNow = false;
            Generate();
        }
    }

    void Start()
    {
        if (Application.isPlaying)
        {
            Generate();
        }
    }

    [ContextMenu("Generate Scene")]
    public void Generate()
    {
        ClearOld();
        CreateMaterials();

        var root = new GameObject("Classroom_Scene");
        BuildRoom(root);
        BuildTeacherArea(root);
        BuildStudentRows(root);
        BuildLighting(root);
        PlaceSpawnPoint(root);

        Debug.Log("[ClassroomGenerator] Structural classroom scene generated.");
    }

    void CreateMaterials()
    {
        matFloor = Mat(new Color(0.85f, 0.82f, 0.78f), texFloor, textureTiling * 2f);
        matWall = Mat(new Color(0.92f, 0.90f, 0.85f), texWall, textureTiling);
        matCeiling = Mat(new Color(0.96f, 0.96f, 0.94f));
        matWood = Mat(new Color(0.72f, 0.50f, 0.28f), texWood, textureTiling);
        matDesk = Mat(new Color(0.80f, 0.60f, 0.35f), texWood, textureTiling);
        matGray = Mat(new Color(0.75f, 0.73f, 0.70f));
        matChair = Mat(new Color(0.25f, 0.25f, 0.28f));
        matBoard = Mat(new Color(0.97f, 0.97f, 0.97f));
        matBoardFrame = Mat(new Color(0.45f, 0.30f, 0.15f));
    }

    Material Mat(Color c, Texture2D tex = null, float tiling = 1f)
    {
        var material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        if (material.shader.name == "Hidden/InternalErrorShader")
        {
            material = new Material(Shader.Find("Standard"));
        }

        material.color = c;
        if (tex != null)
        {
            material.mainTexture = tex;
            material.mainTextureScale = new Vector2(tiling, tiling);
        }

        return material;
    }

    void ClearOld()
    {
        var old = GameObject.Find("Classroom_Scene");
        if (old != null)
        {
            DestroyImmediate(old);
        }
    }

    void BuildRoom(GameObject root)
    {
        var env = Child("Room", root);
        Cube("Floor", env, V(0f, -0.15f, roomDepth / 2f), V(roomWidth, 0.3f, roomDepth), matFloor);
        Cube("Wall_Front", env, V(0f, roomHeight / 2f, 0f), V(roomWidth, roomHeight, 0.2f), matWall);
        Cube("Wall_Back", env, V(0f, roomHeight / 2f, roomDepth), V(roomWidth, roomHeight, 0.2f), matWall);
        Cube("Wall_Left", env, V(-roomWidth / 2f, roomHeight / 2f, roomDepth / 2f), V(0.2f, roomHeight, roomDepth), matWall);
        Cube("Wall_Right", env, V(roomWidth / 2f, roomHeight / 2f, roomDepth / 2f), V(0.2f, roomHeight, roomDepth), matWall);
        Cube("Ceiling", env, V(0f, roomHeight, roomDepth / 2f), V(roomWidth, 0.2f, roomDepth), matCeiling);
    }

    void BuildTeacherArea(GameObject root)
    {
        var teacherArea = Child("TeacherArea", root);

        float boardWidth = 3.5f;
        float boardHeight = 1.4f;
        float boardY = 1.5f;

        Cube("Whiteboard", teacherArea, V(0f, boardY, 0.15f), V(boardWidth, boardHeight, 0.04f), matBoard);
        Cube("WhiteboardFrame", teacherArea, V(0f, boardY, 0.13f), V(boardWidth + 0.12f, boardHeight + 0.12f, 0.03f), matBoardFrame);
    }

    void BuildStudentRows(GameObject root)
    {
        var rowRoot = Child("StudentRows", root);

        float usableWidth = roomWidth - 0.4f;
        float seatingWidth = usableWidth - corridorWidth * 2f;
        float leftWidth = seatingWidth * leftBlockRatio;
        float centerWidth = seatingWidth * centerBlockRatio;
        float rightWidth = seatingWidth * (1f - leftBlockRatio - centerBlockRatio);

        float originX = -usableWidth / 2f;
        float leftCenterX = originX + leftWidth / 2f;
        float centerCenterX = originX + leftWidth + corridorWidth + centerWidth / 2f;
        float rightCenterX = originX + leftWidth + corridorWidth + centerWidth + corridorWidth + rightWidth / 2f;

        float startZ = teacherAreaDepth + 1.0f;

        for (int row = 0; row < rowCount; row++)
        {
            float baseZ = startZ + row * rowSpacing;
            var rowGO = Child($"Row_{row}", rowRoot);

            BuildCounterBlock(rowGO, $"L{row}", leftCenterX, leftWidth, baseZ);
            BuildCounterBlock(rowGO, $"C{row}", centerCenterX, centerWidth, baseZ);
            BuildCounterBlock(rowGO, $"R{row}", rightCenterX, rightWidth, baseZ);
        }
    }

    void BuildCounterBlock(GameObject parent, string name, float centerX, float blockWidth, float baseZ)
    {
        // Taller desk body so the rows read more like classroom benches.
        float bodyHeight = 0.74f;
        float bodyDepth = 0.50f;
        float bodyY = bodyHeight / 2f;
        float bodyCenterZ = baseZ + bodyDepth / 2f;
        float halfWidth = blockWidth / 2f;

        Cube($"Body_{name}", parent,
            V(centerX, bodyY, bodyCenterZ),
            V(blockWidth, bodyHeight, bodyDepth), matGray);

        float deskThickness = 0.05f;
        float deskY = bodyHeight + deskThickness / 2f;
        float deskWidth = blockWidth + 0.15f;
        float deskDepth = bodyDepth + 0.08f;
        Cube($"Desk_{name}", parent,
            V(centerX, deskY, bodyCenterZ),
            V(deskWidth, deskThickness, deskDepth), matDesk);

        Cube($"SideL_{name}", parent,
            V(centerX - halfWidth - 0.03f, bodyY, bodyCenterZ),
            V(0.06f, bodyHeight + 0.02f, bodyDepth + 0.04f), matWood);

        Cube($"SideR_{name}", parent,
            V(centerX + halfWidth + 0.03f, bodyY, bodyCenterZ),
            V(0.06f, bodyHeight + 0.02f, bodyDepth + 0.04f), matWood);

        // Raise the seat proportionally with the taller desk body.
        float seatY = 0.48f;
        float backZ = baseZ + rowSpacing - 0.08f;
        float seatDepth = 0.30f;
        float seatZ = backZ - seatDepth / 2f - 0.02f;
        float chairWidth = 0.50f;
        float gapWidth = 0.04f;
        float totalUnit = chairWidth + gapWidth;
        int chairCount = Mathf.FloorToInt(blockWidth / totalUnit);
        float startX = centerX - (chairCount * totalUnit - gapWidth) / 2f + chairWidth / 2f;

        for (int chair = 0; chair < chairCount; chair++)
        {
            float chairX = startX + chair * totalUnit;
            string chairName = $"{name}_Ch{chair}";

            Cube($"Seat_{chairName}", parent,
                V(chairX, seatY, seatZ), V(chairWidth, 0.05f, seatDepth), matDesk);

            float legHeight = seatY - 0.025f;
            float legY = legHeight / 2f;
            Cube($"SeatLeg_{chairName}", parent,
                V(chairX, legY, seatZ), V(0.04f, legHeight, 0.04f), matChair);

            float backHeight = 0.38f;
            Cube($"Back_{chairName}", parent,
                V(chairX, seatY + backHeight / 2f, backZ), V(chairWidth, backHeight, 0.04f), matDesk);

            Cube($"BackLeg_{chairName}", parent,
                V(chairX, legY, backZ), V(0.04f, legHeight, 0.04f), matChair);
        }
    }

    void BuildLighting(GameObject root)
    {
        var lightingRoot = Child("Lighting", root);

        float[] xPositions = { -2.5f, 2.5f };
        float[] zPositions = { 3f, 6f, 9f };

        int index = 0;
        foreach (float x in xPositions)
        {
            foreach (float z in zPositions)
            {
                AddLight($"Light_{index}", lightingRoot,
                    V(x, roomHeight - 0.3f, z),
                    LightType.Spot, 5f, 12f,
                    new Color(1f, 0.95f, 0.88f));

                Cube($"Housing_{index}", lightingRoot,
                    V(x, roomHeight - 0.11f, z),
                    V(0.5f, 0.05f, 0.5f), matCeiling);

                index++;
            }
        }

        AddLight("BoardLight", lightingRoot,
            V(0f, roomHeight - 0.3f, 1f),
            LightType.Spot, 4f, 8f,
            new Color(1f, 0.96f, 0.90f));
    }

    void PlaceSpawnPoint(GameObject root)
    {
        var spawnPoint = new GameObject("SpawnPoint");
        spawnPoint.transform.SetParent(root.transform, false);
        spawnPoint.transform.position = V(0f, 0.1f, 1.8f);
        spawnPoint.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
    }

    GameObject Cube(string name, GameObject parent, Vector3 position, Vector3 scale, Material material)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.SetParent(parent.transform, false);
        go.transform.position = position;
        go.transform.localScale = scale;
        go.GetComponent<Renderer>().material = material;
        return go;
    }

    GameObject Child(string name, GameObject parent)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent.transform, false);
        return go;
    }

    Vector3 V(float x, float y, float z) => new Vector3(x, y, z);

    void AddLight(string name, GameObject parent, Vector3 position,
                  LightType type, float intensity, float range, Color color)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent.transform, false);
        go.transform.position = position;
        if (type == LightType.Spot)
        {
            go.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        }

        var lightComponent = go.AddComponent<Light>();
        lightComponent.type = type;
        lightComponent.intensity = intensity;
        lightComponent.range = range;
        lightComponent.color = color;
        if (type == LightType.Spot)
        {
            lightComponent.spotAngle = 100f;
        }
        lightComponent.shadows = LightShadows.Soft;
    }
}


