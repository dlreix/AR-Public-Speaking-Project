using System.Collections;
using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Spawns audience characters on detected seats in the scene.
/// Hand-heals URP materials and manages variety.
/// </summary>
public class AudienceSpawner : MonoBehaviour
{
    [Header("Prefabs")]
    public List<GameObject> audiencePrefabs;

    [Header("Seating Layout (grid fallback)")]
    public int rowCount = 5;
    public int colCount = 7;
    public float rowSpacing = 2.0f;
    public float colSpacing = 1.5f;

    [Header("Start Position (grid fallback)")]
    public Vector3 startPosition = new Vector3(-4.5f, 0, 2f);

    [Header("Audience Controller")]
    public AudienceBehaviorController controller;

    [Header("Placement Tuning")]
    [Tooltip("Seat backward inset so character does not float over front edge")]
    public float seatBackOffset = 0.05f;

    [Header("Optional Seated Overrides")]
    public AnimationClip seatedIdleClip;
    public AnimationClip seatedClapClip;

    private RuntimeAnimatorController sharedAnimatorController;

    // ------------------------------------------------------------------
    IEnumerator Start()
    {
        // Wait until end of the frame so any procedural gen is done
        yield return new WaitForEndOfFrame();
        
        FixAllSceneMaterials();
        SpawnAudience();
    }

    // ==================================================================
    //  MAIN SPAWN
    // ==================================================================
    public void SpawnAudience()
    {
        // 1) Clean previous spawns
        foreach (var m in controller.audienceMembers)
            if (m != null) Destroy(m.gameObject);
        controller.audienceMembers.Clear();

        // 2) Cleanup static characters (Ghost characters)
        GameObject[] allObjects = FindObjectsByType<GameObject>(FindObjectsSortMode.None);
        foreach (var go in allObjects)
        {
            string n = go.name.ToLower();
            if (n.Contains("sitting") || n.Contains("student") || n.Contains("audience_") || n.Contains("character_"))
            {
                if (go.hideFlags == HideFlags.None && go.transform.parent == null) Destroy(go);
            }
        }

        EnsureControllerReferences();

        // HER ZAMAN keşif yap (Inspector'daki listeyi temizleyip klasörden güncel hali çek)
        audiencePrefabs = LoadCharacterPrefabs();

        if (audiencePrefabs.Count == 0)
        {
            Debug.LogError("[AudienceSpawner] No prefabs found in Models folder!");
            return;
        }

        // Find seats
        List<GameObject> allSeats = FindAllSeats();

        if (allSeats.Count == 0)
        {
            Debug.LogWarning("[AudienceSpawner] No seats found — grid fallback.");
            SpawnGrid();
            return;
        }

        // Shuffle seats
        for (int i = allSeats.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            var tmp = allSeats[i]; allSeats[i] = allSeats[j]; allSeats[j] = tmp;
        }

        int maxToSpawn = Mathf.Min(GetAudienceCount(), allSeats.Count);

        for (int i = 0; i < maxToSpawn; i++)
        {
            GameObject seat = allSeats[i];
            int prefabIdx = Random.Range(0, audiencePrefabs.Count);

            // ── 1) Instantiate ──
            Quaternion faceForward = Quaternion.Euler(0, seat.transform.eulerAngles.y + 180f, 0);
            GameObject memberObj = Instantiate(audiencePrefabs[prefabIdx], Vector3.zero, faceForward);
            
            // Fix Materials & Height
            FixInstanceMaterials(memberObj);
            FixProceduralHeight(memberObj);

            // ── 2) Animator & Logic Components ──
            Animator anim = memberObj.GetComponent<Animator>();
            if (anim == null) anim = memberObj.AddComponent<Animator>();
            anim.applyRootMotion = false;
            anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            if (sharedAnimatorController != null) anim.runtimeAnimatorController = sharedAnimatorController;

            // Procedural Animator (Kritik: Hataları çözer ve animasyon geçişlerini sağlar)
            ProceduralAudienceAnimator procAnim = memberObj.GetComponent<ProceduralAudienceAnimator>();
            if (procAnim == null) procAnim = memberObj.AddComponent<ProceduralAudienceAnimator>();
            
            AudienceMember am = memberObj.GetComponent<AudienceMember>();
            if (am == null) am = memberObj.AddComponent<AudienceMember>();
            am.animator = anim;
            am.proceduralAnimator = procAnim;
            // Not: procAnim içindeki _animator private olduğu için atamayı Awake kendisi yapacak.

            // ── 3) Hips Positioning ──
            anim.Rebind();
            anim.Update(0f);

            Bounds seatBounds;
            if (!TryGetBounds(seat, out seatBounds)) seatBounds = new Bounds(seat.transform.position, new Vector3(0.5f, 0.05f, 0.3f));

            float seatSurface = seatBounds.max.y;
            Vector3 seatInset = GetSeatBackwardOffset(seat);

            Transform hips = FindBoneRecursive(memberObj.transform, "hips");
            if (hips != null)
            {
                Vector3 desiredHips = new Vector3(
                    seatBounds.center.x + seatInset.x,
                    seatSurface + 0.20f, // 0.20f height for 1.95m characters
                    seatBounds.center.z + seatInset.z
                );
                Vector3 delta = desiredHips - hips.position;
                memberObj.transform.position += delta;
            }
            else
            {
                memberObj.transform.position = seatBounds.center + seatInset + new Vector3(0, -0.45f, 0);
            }

            // ── 4) Logic ──
            ApplyRandomPersonality(am);
            controller.audienceMembers.Add(am);
        }

        Debug.Log($"[AudienceSpawner] Spawned {maxToSpawn} audience on {allSeats.Count} seats.");
    }

    private void SpawnGrid()
    {
        int count = GetAudienceCount();
        int spawned = 0;
        float[] blockOffsets = { -5f, 0f, 5f };

        for (int row = 0; row < rowCount && spawned < count; row++)
        {
            foreach (float blockX in blockOffsets)
            {
                for (int col = 0; col < 2 && spawned < count; col++)
                {
                    float x = blockX + col * colSpacing;
                    float z = startPosition.z + row * rowSpacing;
                    Vector3 pos = new Vector3(x, startPosition.y, z);

                    int idx = Random.Range(0, audiencePrefabs.Count);
                    GameObject member = Instantiate(audiencePrefabs[idx], pos, Quaternion.Euler(0, 180, 0));

                    FixInstanceMaterials(member);
                    FixProceduralHeight(member);

                    ProceduralAudienceAnimator procAnim = member.GetComponent<ProceduralAudienceAnimator>();
                    if (procAnim == null) procAnim = member.AddComponent<ProceduralAudienceAnimator>();

                    AudienceMember am = member.GetComponent<AudienceMember>();
                    if (am == null) am = member.AddComponent<AudienceMember>();
                    am.proceduralAnimator = procAnim;

                    // Animator setup
                    Animator anim = member.GetComponent<Animator>();
                    if (anim == null) anim = member.AddComponent<Animator>();
                    anim.runtimeAnimatorController = sharedAnimatorController;
                    anim.applyRootMotion = false;

                    controller.audienceMembers.Add(am);
                    spawned++;
                }
            }
        }
    }

    // ==================================================================
    //  MATERIAL & HEIGHT HEALING
    // ==================================================================
    [ContextMenu("Fix All Pink Assets in Project")]
    public void FixProjectMaterials()
    {
#if UNITY_EDITOR
        string modelsPath = "Assets/AudienceSimulation_Arda/Models";
        string[] guids = AssetDatabase.FindAssets("t:Model", new[] { modelsPath });
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            ModelImporter importer = AssetImporter.GetAtPath(path) as ModelImporter;
            if (importer != null)
            {
                // This is a complex operation, but we can at least try to fix materials 
                // in the scene by scanning all materials in those subfolders.
            }
        }
        
        string[] matGuids = AssetDatabase.FindAssets("t:Material", new[] { modelsPath });
        Shader urpLit = Shader.Find("Universal Render Pipeline/Lit");
        foreach (string guid in matGuids)
        {
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid));
            if (mat != null && (mat.shader.name.Contains("Standard") || mat.shader.name.Contains("Error")))
            {
                mat.shader = urpLit;
                EditorUtility.SetDirty(mat);
            }
        }
        AssetDatabase.SaveAssets();
        Debug.Log("[AudienceSpawner] Project materials fixed! (Pink icons should update)");
#endif
    }

    private void FixInstanceMaterials(GameObject target)
    {
        Shader urpLit = Shader.Find("Universal Render Pipeline/Lit");
        if (urpLit == null) urpLit = Shader.Find("Universal Render Pipeline/Simple Lit");
        if (urpLit == null) return;

#if UNITY_EDITOR
        string modelName = target.name.Replace("(Clone)", "").Trim();
        // Smarter discovery: find the FBX file path to get its specific folder
        string[] guids = AssetDatabase.FindAssets(modelName + " t:Model");
        string modelDir = "";
        foreach(var guid in guids) {
            string p = AssetDatabase.GUIDToAssetPath(guid);
            if (p.ToLower().Contains("models") && !p.Contains("@")) {
                modelDir = System.IO.Path.GetDirectoryName(p);
                break;
            }
        }
#endif

        Renderer[] renderers = target.GetComponentsInChildren<Renderer>(true);
        foreach (Renderer r in renderers)
        {
            Material[] mats = r.materials;
            for (int i = 0; i < mats.Length; i++)
            {
                Material mat = mats[i];
                if (mat == null) continue;
                
                if (mat.shader.name.Contains("Standard") || mat.shader.name.Contains("Error") || mat.shader.name.Contains("Lit"))
                {
                    Texture mainTex = mat.mainTexture;
#if UNITY_EDITOR
                    if (mainTex == null && !string.IsNullOrEmpty(modelDir))
                    {
                        string matName = mat.name.ToLower();
                        // Search ONLY in the model's specific folder first
                        string[] files = System.IO.Directory.GetFiles(modelDir, "*.*", System.IO.SearchOption.AllDirectories);
                        Texture tex1001 = null, tex1002 = null, anyDiff = null;
                        foreach (string f in files)
                        {
                            string lowF = f.ToLower();
                            if (lowF.EndsWith(".png") || lowF.EndsWith(".jpg") || lowF.EndsWith(".tga"))
                            {
                                string assetP = f.Replace(Application.dataPath, "Assets").Replace("\\", "/");
                                if (lowF.Contains("1001") || lowF.Contains("body")) tex1001 = AssetDatabase.LoadAssetAtPath<Texture>(assetP);
                                if (lowF.Contains("1002") || lowF.Contains("diffuse") || lowF.Contains("clothe")) {
                                    Texture t = AssetDatabase.LoadAssetAtPath<Texture>(assetP);
                                    if (lowF.Contains("1002")) tex1002 = t;
                                    anyDiff = t;
                                }
                            }
                        }
                        if (matName.Contains("body")) mainTex = tex1001 ?? anyDiff;
                        else if (i == 0 && tex1001 != null) mainTex = tex1001; // Usually first mat is body
                        else mainTex = tex1002 ?? anyDiff;
                    }
#endif
                    mat.shader = urpLit;
                    if (mainTex != null) { 
                        mat.SetTexture("_BaseMap", mainTex); 
                        mat.SetColor("_BaseColor", Color.white); 
                    }
                    else mat.SetColor("_BaseColor", new Color(0.85f, 0.85f, 0.85f));

                    mat.SetFloat("_Smoothness", 0.0f); // Prevents "Shiny Black" look
                    mat.SetFloat("_Metallic", 0.0f);
                }
            }
        }
    }

    private void FixProceduralHeight(GameObject target)
    {
        Bounds bounds;
        if (TryGetBounds(target, out bounds))
        {
            float currentHeight = Mathf.Max(bounds.size.y, 0.01f);
            float targetHeight = 1.95f; // Balanced height (Ellerin masaya yetismesi icin)
            float scaleFactor = targetHeight / currentHeight;
            target.transform.localScale *= Mathf.Clamp(scaleFactor, 0.01f, 100.0f);
        }
    }

    public void FixAllSceneMaterials()
    {
        Shader urpLit = Shader.Find("Universal Render Pipeline/Lit");
        if (urpLit == null) urpLit = Shader.Find("Universal Render Pipeline/Simple Lit");
        if (urpLit == null) return;

        Renderer[] allRenderers = FindObjectsByType<Renderer>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (Renderer r in allRenderers)
        {
            Material[] mats = r.sharedMaterials;
            bool changed = false;
            for (int i = 0; i < mats.Length; i++)
            {
                Material mat = mats[i];
                if (mat == null) continue;

                // Purple/Pink check (Broken shader)
                if (mat.shader.name.Contains("Error") || mat.shader.name.Contains("Standard") || mat.shader.name.Contains("InternalError"))
                {
                    Texture tex = mat.mainTexture;
                    mat.shader = urpLit;
                    if (tex != null) mat.SetTexture("_BaseMap", tex);
                    mat.SetColor("_BaseColor", Color.white);
                    changed = true;
                }
            }
            if (changed) r.sharedMaterials = mats;
        }
    }

    // ==================================================================
    //  DISCOVERY
    // ==================================================================
    private List<GameObject> FindAllSeats()
    {
        List<GameObject> seats = new List<GameObject>();
        GameObject[] allObjects = FindObjectsByType<GameObject>(FindObjectsSortMode.None);
        foreach (GameObject go in allObjects)
        {
            string n = go.name.ToLower();
            if (n.Contains("seat") || n.Contains("bench") || n.Contains("chair"))
            {
                if (!n.Contains("leg") && !n.Contains("side") && !n.Contains("back") && !n.Contains("frame"))
                {
                    Renderer r = go.GetComponent<Renderer>();
                    if (r != null && r.bounds.size.x > 1.2f) { // Subdivide benches
                        Transform existing = go.transform.Find("VirtualSeats");
                        if (existing != null) { foreach (Transform child in existing) seats.Add(child.gameObject); }
                        else {
                            GameObject vsRoot = new GameObject("VirtualSeats"); vsRoot.transform.parent = go.transform; vsRoot.transform.localPosition = Vector3.zero;
                            float seatWidth = 0.65f; int count = Mathf.FloorToInt(r.bounds.size.x / seatWidth);
                            float startX = r.bounds.center.x - (count * seatWidth) / 2f + seatWidth / 2f;
                            for (int i = 0; i < count; i++) {
                                if (count == 3 && i == 1) continue;
                                GameObject vSeat = GameObject.CreatePrimitive(PrimitiveType.Cube); vSeat.name = "Seat_Virtual_" + i; vSeat.transform.parent = vsRoot.transform;
                                vSeat.transform.position = new Vector3(startX + i * seatWidth, r.bounds.center.y, r.bounds.center.z);
                                vSeat.transform.rotation = go.transform.rotation; vSeat.transform.localScale = new Vector3(seatWidth, r.bounds.size.y, r.bounds.size.z);
                                vSeat.GetComponent<Renderer>().enabled = false; seats.Add(vSeat);
                            }
                        }
                    }
                    else
                    {
                        // Duplicate prevention check
                        bool exists = false;
                        foreach(var s in seats) if(Vector3.Distance(s.transform.position, go.transform.position) < 0.1f) exists = true;
                        if(!exists) seats.Add(go);
                    }
                }
            }
        }
        return seats;
    }

    private List<GameObject> LoadCharacterPrefabs()
    {
        List<GameObject> prefabs = new List<GameObject>();
#if UNITY_EDITOR
        // SADECE Arda klasöründeki Mixamo modellerine bak (Zombi modelleri dışarıda bırak)
        string folder = "Assets/AudienceSimulation_Arda/Models";
        
        if (System.IO.Directory.Exists(Application.dataPath + "/" + folder.Replace("Assets/", "")))
        {
            string[] guids = AssetDatabase.FindAssets("t:Model t:Prefab", new[] { folder });
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                string fileName = System.IO.Path.GetFileName(path).ToLower();

                // Skip animations, materials, zombi/sitting models and non-humanoids
                if (fileName.Contains("@") || fileName.Contains("material") || 
                    fileName.Contains("anim") || fileName.Contains("sitting")) continue;

                GameObject go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (go != null && go.GetComponentInChildren<SkinnedMeshRenderer>() != null)
                {
                    if (!prefabs.Contains(go)) prefabs.Add(go);
                }
            }
        }
        
        Debug.Log($"[AudienceSpawner] Pure Mixamo Discovery: Found {prefabs.Count} unique character models.");
#endif
        return prefabs;
    }

    [ContextMenu("Force Spawn Now")]
    public void ForceSpawn() { SpawnAudience(); }

    private void ApplyRandomPersonality(AudienceMember am)
    {
        switch (controller.currentStressLevel)
        {
            case StressLevel.Easy: am.personalWpmTolerance = Random.Range(-40f, -10f); am.personalEyeContactTolerance = Random.Range(-0.2f, -0.05f); break;
            case StressLevel.Medium: am.personalWpmTolerance = Random.Range(-10f, 10f); am.personalEyeContactTolerance = Random.Range(-0.08f, 0.08f); break;
            case StressLevel.Hard: am.personalWpmTolerance = Random.Range(10f, 40f); am.personalEyeContactTolerance = Random.Range(0.1f, 0.3f); break;
        }
    }

    private int GetAudienceCount()
    {
        // Special case for Meeting Room
        if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().name.ToLower().Contains("meeting")) return 8;
        
        switch (controller.currentStressLevel)
        {
            case StressLevel.Easy: return 12;
            case StressLevel.Medium: return 30;
            case StressLevel.Hard: return 60;
            default: return 30;
        }
    }

    private Vector3 GetSeatBackwardOffset(GameObject seat) { Vector3 b = seat.transform.forward; b.y = 0; b.Normalize(); return b * seatBackOffset; }

    private bool TryGetBounds(GameObject target, out Bounds bounds)
    {
        Renderer[] rs = target.GetComponentsInChildren<Renderer>();
        if (rs.Length > 0) { bounds = rs[0].bounds; for (int i = 1; i < rs.Length; i++) bounds.Encapsulate(rs[i].bounds); return true; }
        bounds = new Bounds(Vector3.zero, Vector3.zero); return false;
    }

    private Transform FindBoneRecursive(Transform current, string boneName)
    {
        if (current.name.ToLower().Contains(boneName.ToLower())) return current;
        foreach (Transform child in current) { Transform f = FindBoneRecursive(child, boneName); if (f != null) return f; }
        return null;
    }

    private void EnsureControllerReferences()
    {
#if UNITY_EDITOR
        if (sharedAnimatorController == null) {
            string[] guids = AssetDatabase.FindAssets("AudienceAnimator t:RuntimeAnimatorController");
            if (guids.Length > 0) sharedAnimatorController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(AssetDatabase.GUIDToAssetPath(guids[0]));
        }
#endif
    }
}