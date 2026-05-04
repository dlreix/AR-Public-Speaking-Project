using System.Collections;
using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Spawns audience characters on detected seats in the scene.
///
/// POSITIONING STRATEGY (the "Remy Standard"):
///   Remy (scale 1, height ~1.7 m) at Y = 0 with a sitting-idle animation
///   has its hips at ~0.46 m, very close to the classroom seat surface
///   at 0.505 m.  This looks correct.
///
///   We replicate this for ALL characters:
///     1. Keep the prefab's original scale (Remy = 1, Ch07/Ch33 = 2).
///     2. Measure T-pose height in world space and scale with *= so every
///        character is exactly 1.70 m tall (= Remy height).
///     3. Place the character root at FLOOR LEVEL beneath the seat.
///        The sitting animation naturally puts hips at ~0.46 m from root,
///        which lands right on the seat surface.
///
///   Floor level is derived from the seat geometry:
///     • Classroom  — Seat surface is 0.505 m above Y = 0 floor.
///       floorY = seatSurface − 0.505 ≈ 0.
///     • Conference — Bench surface is benchY + 0.025, and the tier
///       surface is benchY − 0.40 + tierHeight/2.
///       We approximate: floorY = seatSurface − 0.505.
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
    public float seatBackOffset = 0.02f;

    [Header("Optional Seated Overrides")]
    public AnimationClip seatedIdleClip;
    public AnimationClip seatedClapClip;

    private RuntimeAnimatorController sharedAnimatorController;

    // ------------------------------------------------------------------
    IEnumerator Start()
    {
        // Wait until end of the very first frame so any procedural
        // room generation (like ClassroomGenerator) has finished building seats.
        // This avoids a visible 0.15s delay/pop-in.
        yield return new WaitForEndOfFrame();
        SpawnAudience();
    }

    // ==================================================================
    //  MAIN SPAWN
    // ==================================================================
    public void SpawnAudience()
    {
        // Clean old
        foreach (var m in controller.audienceMembers)
            if (m != null) Destroy(m.gameObject);
        controller.audienceMembers.Clear();

        EnsureControllerReferences();

        // Load prefabs if list is empty
        if (audiencePrefabs == null || audiencePrefabs.Count == 0)
        {
            audiencePrefabs = LoadCharacterPrefabs();
            if (audiencePrefabs.Count == 0)
            {
                Debug.LogError("[AudienceSpawner] No audience prefabs assigned!");
                return;
            }
        }

        // Find seats
        List<GameObject> allSeats = FindAllSeats();
        if (allSeats.Count == 0)
        {
            Debug.LogWarning("[AudienceSpawner] No seats found — grid fallback.");
            SpawnGrid();
            return;
        }

        // Shuffle
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

            // ── 2) Animator (Base Sitting Animation) ──
            Animator anim = memberObj.GetComponent<Animator>();
            if (anim == null) anim = memberObj.AddComponent<Animator>();
            anim.applyRootMotion = false;
            anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            
            // ASIL KONTROLCÜYÜ DİREKT ATA (Silme/Ezme yapma)
            if (sharedAnimatorController != null)
                anim.runtimeAnimatorController = sharedAnimatorController;

            // ── 3) Normalize height (Shrink slightly so they fit chairs) ──
            Bounds bounds;
            if (TryGetBounds(memberObj, out bounds))
            {
                float currentHeight = Mathf.Max(bounds.size.y, 0.01f);
                float targetHeight = 2.35f; // Sınıf masaları gerçek hayattan çok daha büyük çizildiği için karakter boyunu tam orantılı yapıyoruz (Eski orana döndürdüm)
                float scaleFactor = targetHeight / currentHeight;
                // Mixamo modelleri inç/cm yüzünden x100 veya x0.01 gelebilir, o yüzden sınırı çok genişlettik
                scaleFactor = Mathf.Clamp(scaleFactor, 0.01f, 200.0f);
                memberObj.transform.localScale *= scaleFactor;
            }

            // ── 4) Force position via hips (Evaluated Animation alignment) ──
            anim.Rebind();
            if (anim.layerCount > 0)
            {
                anim.Play(anim.GetCurrentAnimatorStateInfo(0).fullPathHash, 0, Random.value);
            }
            anim.Update(0f);

            Bounds seatBounds;
            if (!TryGetBounds(seat, out seatBounds))
                seatBounds = new Bounds(seat.transform.position, new Vector3(0.5f, 0.05f, 0.3f));

            float seatSurface = seatBounds.max.y;
            Vector3 seatInset = GetSeatBackwardOffset(seat);

            // Move the character so its hips rest slightly above the seat.
            Transform hips = null;
            if (anim != null && anim.isHuman)
                hips = anim.GetBoneTransform(HumanBodyBones.Hips);
            
            if (hips == null)
                hips = FindBoneRecursive(memberObj.transform, "hips");

            if (hips != null)
            {
                // Desired hips position: horizontally at seat centre,
                // vertically 25cm above the seat surface so hands reach the desk! (Boyut büyüdüğü için 25cm yaptık)
                Vector3 desiredHips = new Vector3(
                    seatBounds.center.x + seatInset.x,
                    seatSurface + 0.25f,
                    seatBounds.center.z + seatInset.z
                );
                Vector3 delta = desiredHips - hips.position;
                memberObj.transform.position += delta;
            }
            else
            {
                // Fallback: floor-based, but lifted by 25cm
                float floorY = seatSurface - 0.505f + 0.25f;
                memberObj.transform.position = new Vector3(
                    seatBounds.center.x + seatInset.x,
                    floorY,
                    seatBounds.center.z + seatInset.z
                );
            }

            // ── 5) Add components (Sıralama Krittik: Önce Motor, Sonra Beyin) ──
            var procAnim = memberObj.GetComponent<ProceduralAudienceAnimator>();
            if (procAnim == null) procAnim = memberObj.AddComponent<ProceduralAudienceAnimator>();
            procAnim.enabled = true; // Kesinlikle aktif olmalı

            var am = memberObj.GetComponent<AudienceMember>();
            if (am == null) am = memberObj.AddComponent<AudienceMember>();
            am.enabled = true;

            // Stress-level personality
            switch (controller.currentStressLevel)
            {
                case StressLevel.Easy:
                    am.personalWpmTolerance = Random.Range(-30f, -5f);
                    am.personalEyeContactTolerance = Random.Range(-0.2f, -0.05f);
                    break;
                case StressLevel.Medium:
                    am.personalWpmTolerance = Random.Range(-10f, 10f);
                    am.personalEyeContactTolerance = Random.Range(-0.08f, 0.08f);
                    break;
                case StressLevel.Hard:
                    am.personalWpmTolerance = Random.Range(10f, 40f);
                    am.personalEyeContactTolerance = Random.Range(0.1f, 0.3f);
                    break;
            }

            controller.audienceMembers.Add(am);
        }

        Debug.Log($"[AudienceSpawner] Spawned {maxToSpawn} audience on {allSeats.Count} seats.");
    }

    // ==================================================================
    //  GRID FALLBACK (original layout, no seats)
    // ==================================================================
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

                    Animator anim = member.GetComponent<Animator>();
                    if (anim == null) anim = member.AddComponent<Animator>();
                    anim.applyRootMotion = false;
                    anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                    
                    if (sharedAnimatorController != null)
                        anim.runtimeAnimatorController = sharedAnimatorController;

                    var procAnim = member.GetComponent<ProceduralAudienceAnimator>();
                    if (procAnim == null) procAnim = member.AddComponent<ProceduralAudienceAnimator>();
                    procAnim.enabled = true;

                    AudienceMember am = member.GetComponent<AudienceMember>();
                    if (am == null) am = member.AddComponent<AudienceMember>();
                    am.enabled = true;

                    switch (controller.currentStressLevel)
                    {
                        case StressLevel.Easy:
                            am.personalWpmTolerance = Random.Range(-30f, -5f);
                            am.personalEyeContactTolerance = Random.Range(-0.2f, -0.05f);
                            break;
                        case StressLevel.Medium:
                            am.personalWpmTolerance = Random.Range(-10f, 10f);
                            am.personalEyeContactTolerance = Random.Range(-0.08f, 0.08f);
                            break;
                        case StressLevel.Hard:
                            am.personalWpmTolerance = Random.Range(10f, 40f);
                            am.personalEyeContactTolerance = Random.Range(0.1f, 0.3f);
                            break;
                    }

                    controller.audienceMembers.Add(am);
                    spawned++;
                }
            }
        }
    }

    // ==================================================================
    //  SEAT DISCOVERY
    // ==================================================================
    private List<GameObject> FindAllSeats()
    {
        List<GameObject> seats = new List<GameObject>();
        GameObject[] allObjects = FindObjectsByType<GameObject>(FindObjectsSortMode.None);

        foreach (GameObject go in allObjects)
        {
            string n = go.name.ToLower();
            // ClassroomGenerator creates "Seat_*"
            // ConferenceHallGenerator creates "Bench_*"
            if (n.StartsWith("seat") || n.StartsWith("bench"))
            {
                // Exclude structural components (legs, sides, backs)
                if (!n.Contains("leg") && !n.Contains("side") && !n.Contains("back")
                    && !n.Contains("frame") && !n.Contains("body"))
                {
                    Renderer r = go.GetComponent<Renderer>();
                    if (r != null && r.bounds.size.x > 1.2f) // Wide bench, subdivide!
                    {
                        Transform existing = go.transform.Find("VirtualSeats");
                        if (existing == null)
                        {
                            GameObject vsRoot = new GameObject("VirtualSeats");
                            vsRoot.transform.parent = go.transform;
                            vsRoot.transform.localPosition = Vector3.zero;

                            float seatWidth = 0.65f;
                            int count = Mathf.FloorToInt(r.bounds.size.x / seatWidth);
                            float startX = r.bounds.center.x - (count * seatWidth) / 2f + seatWidth / 2f;
                            for (int i = 0; i < count; i++)
                            {
                                // Leave the middle seat empty for benches that fit exactly 3 people
                                if (count == 3 && i == 1) continue;

                                GameObject vSeat = GameObject.CreatePrimitive(PrimitiveType.Cube);
                                vSeat.name = "Seat_Virtual_" + i;
                                vSeat.transform.parent = vsRoot.transform;
                                vSeat.transform.position = new Vector3(startX + i * seatWidth, r.bounds.center.y, r.bounds.center.z);
                                vSeat.transform.rotation = go.transform.rotation;
                                vSeat.transform.localScale = new Vector3(seatWidth, r.bounds.size.y, r.bounds.size.z);
                                vSeat.GetComponent<Renderer>().enabled = false;
                                seats.Add(vSeat);
                            }
                        }
                        else
                        {
                            foreach (Transform child in existing)
                                seats.Add(child.gameObject);
                        }
                    }
                    else
                    {
                        seats.Add(go);
                    }
                }
            }
        }

        return seats;
    }

    // ==================================================================
    //  HELPERS
    // ==================================================================
    private Vector3 GetSeatBackwardOffset(GameObject seat)
    {
        Vector3 backward = seat.transform.forward;
        if (backward.sqrMagnitude < 0.001f) backward = Vector3.forward;
        backward.y = 0f;
        backward.Normalize();
        return backward * seatBackOffset;
    }

    private List<GameObject> LoadCharacterPrefabs()
    {
        List<GameObject> prefabs = new List<GameObject>();
#if UNITY_EDITOR
        string[] names = { "Ch07_nonPBR", "Ch33_nonPBR", "Remy - T Pose" };
        foreach (string name in names)
        {
            string[] guids = AssetDatabase.FindAssets(name + " t:Prefab");
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab != null) { prefabs.Add(prefab); break; }
            }
        }
#endif
        return prefabs;
    }



    private void EnsureControllerReferences()
    {
#if UNITY_EDITOR
        if (sharedAnimatorController == null)
        {
            string[] guids = AssetDatabase.FindAssets("AudienceAnimator t:RuntimeAnimatorController");
            if (guids.Length > 0)
                sharedAnimatorController =
                    AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(
                        AssetDatabase.GUIDToAssetPath(guids[0]));
        }
        if (seatedIdleClip == null)
        {
            string[] guids = AssetDatabase.FindAssets("Ch07_nonPBR@Sitting Idle t:AnimationClip");
            if (guids.Length > 0)
                seatedIdleClip =
                    AssetDatabase.LoadAssetAtPath<AnimationClip>(
                        AssetDatabase.GUIDToAssetPath(guids[0]));
        }
        if (seatedClapClip == null)
        {
            string[] guids = AssetDatabase.FindAssets("Ch07_nonPBR@Sitting Clap t:AnimationClip");
            if (guids.Length > 0)
                seatedClapClip =
                    AssetDatabase.LoadAssetAtPath<AnimationClip>(
                        AssetDatabase.GUIDToAssetPath(guids[0]));
        }
#endif
    }

    [ContextMenu("Force Spawn Now")]
    public void ForceSpawn() { SpawnAudience(); }

    int GetAudienceCount()
    {
        switch (controller.currentStressLevel)
        {
            case StressLevel.Easy: return 20;
            case StressLevel.Medium: return 50;
            case StressLevel.Hard: return 120;
            default: return 50;
        }
    }

    private bool TryGetBounds(GameObject target, out Bounds bounds)
    {
        Renderer[] renderers = target.GetComponentsInChildren<Renderer>();
        if (renderers.Length > 0)
        {
            bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
                bounds.Encapsulate(renderers[i].bounds);
            return true;
        }
        bounds = new Bounds(Vector3.zero, Vector3.zero);
        return false;
    }

    private Transform FindBoneRecursive(Transform current, string boneName)
    {
        if (current.name.ToLower().Contains(boneName.ToLower()))
            return current;
        foreach (Transform child in current)
        {
            Transform found = FindBoneRecursive(child, boneName);
            if (found != null) return found;
        }
        return null;
    }

#if UNITY_EDITOR
    private void OnValidate() { EnsureControllerReferences(); }
    private void Reset() { OnValidate(); }
#endif
}