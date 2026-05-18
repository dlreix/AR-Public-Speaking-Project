using System.Collections;
using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class AudienceSpawner : MonoBehaviour
{
    [Header("Prefabs")]
    public List<GameObject> audiencePrefabs;

    [Header("Seating Layout (grid fallback)")]
    public int rowCount = 5;
    public int colCount = 7;
    public float rowSpacing = 2.0f;
    public float colSpacing = 1.5f;
    public Vector3 startPosition = new Vector3(-4.5f, 0, 2f);

    [Header("Audience Controller")]
    public AudienceBehaviorController controller;

    [Header("Placement Tuning")]
    public float seatBackOffset = 0.05f;

    [Header("Meeting Room")]
    public float meetingRoomVisualYawOffset = 0f;
    public float meetingRoomMinimumSideDistance = 3f;
    public float meetingRoomMarkerHipsYOffset = 0.18f;
    public float meetingRoomMarkerCharacterHeight = 1.95f;
    public bool drawMeetingRoomFacingDebug = false;

    private RuntimeAnimatorController sharedAnimatorController;

    IEnumerator Start()
    {
        yield return new WaitForEndOfFrame();
        FixAllSceneMaterials();
        SpawnAudience();
    }

    public void SpawnAudience()
    {
        EnsureControllerReferences();
        audiencePrefabs = LoadCharacterPrefabs();
        if (audiencePrefabs.Count == 0)
        {
            Debug.LogWarning("[AudienceSpawner] No audience prefabs were found. Keeping existing scene audience in place.");
            return;
        }

        ClearExistingAudience();

        Transform meetingSeatMarkers = FindMeetingRoomSeatMarkerRoot();
        Transform meetingTable = FindMeetingRoomTable();
        bool isMeetingRoom = meetingTable != null || meetingSeatMarkers != null;
        bool useMeetingSeatMarkers = meetingSeatMarkers != null;
        List<GameObject> allSeats = FindAllSeats(meetingTable, meetingSeatMarkers);
        if (allSeats.Count == 0) { SpawnGrid(); return; }

        // Shuffle
        for (int i = allSeats.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            var tmp = allSeats[i]; allSeats[i] = allSeats[j]; allSeats[j] = tmp;
        }

        int maxToSpawn = Mathf.Min(GetAudienceCount(), allSeats.Count);

        // Meeting room characters face the opposite side of the table, not one shared diagonal.
        Vector3 meetingFocalPoint = new Vector3(-0.044f, 0.75f, 5.65f);
        Transform meetingLookTarget = isMeetingRoom ? EnsureMeetingRoomLookTarget(meetingTable, meetingFocalPoint) : null;
        Dictionary<GameObject, Vector3> meetingFacingDirections = null;
        if (isMeetingRoom)
        {
            meetingFacingDirections = useMeetingSeatMarkers
                ? BuildMeetingRoomMarkerFacingDirections(allSeats)
                : BuildMeetingRoomFacingDirections(allSeats, meetingTable, meetingLookTarget);
        }

        for (int i = 0; i < maxToSpawn; i++)
        {
            GameObject seat = allSeats[i];
            int prefabIdx = Random.Range(0, audiencePrefabs.Count);
            GameObject memberObj = Instantiate(audiencePrefabs[prefabIdx], Vector3.zero, Quaternion.identity);
            if (!isMeetingRoom)
            {
                memberObj.transform.rotation = Quaternion.Euler(0, seat.transform.eulerAngles.y + 180f, 0);
            }
            
            FixInstanceMaterials(memberObj);
            FixProceduralHeight(memberObj, useMeetingSeatMarkers ? meetingRoomMarkerCharacterHeight : 1.95f);

            Animator anim = memberObj.GetComponent<Animator>();
            if (anim == null) anim = memberObj.AddComponent<Animator>();
            anim.applyRootMotion = false;
            anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            if (sharedAnimatorController != null) anim.runtimeAnimatorController = sharedAnimatorController;

            ProceduralAudienceAnimator procAnim = memberObj.GetComponent<ProceduralAudienceAnimator>();
            if (procAnim == null) procAnim = memberObj.AddComponent<ProceduralAudienceAnimator>();
            
            AudienceMember am = memberObj.GetComponent<AudienceMember>();
            if (am == null) am = memberObj.AddComponent<AudienceMember>();
            am.animator = anim; am.proceduralAnimator = procAnim;

            // ── Positioning ──
            anim.Rebind();
            anim.Update(0f);

            Bounds seatBounds;
            if (!TryGetBounds(seat, out seatBounds)) seatBounds = new Bounds(seat.transform.position, new Vector3(0.5f, 0.05f, 0.3f));

            Vector3 seatInset = useMeetingSeatMarkers ? Vector3.zero : GetSeatBackwardOffset(seat);
            
            // HEIGHT LOGIC (Surgical fix for both environments)
            float hipsY = 0;
            if (isMeetingRoom)
            {
                // Toplantı Odası: Pivot bazlı (Daha yüksek)
                hipsY = seat.transform.position.y + meetingRoomMarkerHipsYOffset; 
            }
            else
            {
                // Sınıf/Konferans: Orijinal stabil mantık (Gömülmeyi engeller)
                hipsY = seatBounds.max.y + 0.22f; 
            }

            Transform hips = FindBoneRecursive(memberObj.transform, "hips");
            if (hips != null)
            {
                Vector3 desiredHips = new Vector3(
                    seatBounds.center.x + seatInset.x,
                    hipsY, 
                    seatBounds.center.z + seatInset.z
                );
                Vector3 delta = desiredHips - hips.position;
                memberObj.transform.position += delta;
            }
            else
            {
                memberObj.transform.position = seatBounds.center + seatInset + new Vector3(0, -0.45f, 0);
            }

            if (isMeetingRoom)
            {
                Vector3 facingDirection = GetMeetingRoomSeatFacingDirection(seat, meetingTable, meetingLookTarget, meetingFacingDirections);
                AttachMeetingRoomFacing(memberObj, facingDirection);
            }

            ApplyRandomPersonality(am);
            controller.audienceMembers.Add(am);
        }
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
                    FixInstanceMaterials(member); FixProceduralHeight(member);
                    ProceduralAudienceAnimator procAnim = member.GetComponent<ProceduralAudienceAnimator>();
                    if (procAnim == null) procAnim = member.AddComponent<ProceduralAudienceAnimator>();
                    AudienceMember am = member.GetComponent<AudienceMember>();
                    if (am == null) am = member.AddComponent<AudienceMember>();
                    am.proceduralAnimator = procAnim;
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

    [ContextMenu("Fix All Pink Assets in Project")]
    public void FixProjectMaterials()
    {
#if UNITY_EDITOR
        string modelsPath = "Assets/AudienceSimulation_Arda/Models";
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
#endif
    }

    private void FixInstanceMaterials(GameObject target)
    {
        Shader urpLit = Shader.Find("Universal Render Pipeline/Lit");
        if (urpLit == null) urpLit = Shader.Find("Universal Render Pipeline/Simple Lit");
        if (urpLit == null) return;
#if UNITY_EDITOR
        string modelName = target.name.Replace("(Clone)", "").Trim();
        string[] guids = AssetDatabase.FindAssets(modelName + " t:Model");
        string modelDir = "";
        foreach(var guid in guids) {
            string p = AssetDatabase.GUIDToAssetPath(guid);
            if (p.ToLower().Contains("models") && !p.Contains("@")) { modelDir = System.IO.Path.GetDirectoryName(p); break; }
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
                        string[] files = System.IO.Directory.GetFiles(modelDir, "*.*", System.IO.SearchOption.AllDirectories);
                        Texture tex1001 = null, tex1002 = null, anyDiff = null;
                        foreach (string f in files)
                        {
                            string lowF = f.ToLower();
                            if (lowF.EndsWith(".png") || lowF.EndsWith(".jpg") || lowF.EndsWith(".tga")) {
                                string assetP = f.Replace(Application.dataPath, "Assets").Replace("\\", "/");
                                if (lowF.Contains("1001") || lowF.Contains("body")) tex1001 = AssetDatabase.LoadAssetAtPath<Texture>(assetP);
                                if (lowF.Contains("1002") || lowF.Contains("diffuse") || lowF.Contains("clothe")) {
                                    Texture t = AssetDatabase.LoadAssetAtPath<Texture>(assetP);
                                    if (lowF.Contains("1002")) tex1002 = t;
                                    anyDiff = t;
                                }
                            }
                        }
                        if (mat.name.ToLower().Contains("body")) mainTex = tex1001 ?? anyDiff;
                        else if (i == 0 && tex1001 != null) mainTex = tex1001;
                        else mainTex = tex1002 ?? anyDiff;
                    }
#endif
                    mat.shader = urpLit;
                    if (mainTex != null) { mat.SetTexture("_BaseMap", mainTex); mat.SetColor("_BaseColor", Color.white); }
                    else mat.SetColor("_BaseColor", new Color(0.85f, 0.85f, 0.85f));
                    mat.SetFloat("_Smoothness", 0.0f); mat.SetFloat("_Metallic", 0.0f);
                }
            }
        }
    }

    private void FixProceduralHeight(GameObject target, float targetHeight = 1.95f)
    {
        Bounds bounds;
        if (TryGetBounds(target, out bounds))
        {
            float currentHeight = Mathf.Max(bounds.size.y, 0.01f);
            float scaleFactor = targetHeight / currentHeight;
            target.transform.localScale *= Mathf.Clamp(scaleFactor, 0.01f, 100.0f);
        }
    }

    private void ClearExistingAudience()
    {
        GameObject[] allObjects = FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (GameObject go in allObjects)
        {
            if (go != null && go.hideFlags == HideFlags.None &&
                (go.name == "Sitting" || go.name.StartsWith("Sitting (")))
            {
                DestroyAudienceObject(go);
            }
        }

        AudienceMember[] existingMembers = FindObjectsByType<AudienceMember>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (AudienceMember member in existingMembers)
        {
            if (member != null)
            {
                DestroyAudienceObject(member.gameObject);
            }
        }

        if (controller != null)
        {
            controller.audienceMembers.Clear();
        }
    }

    private void DestroyAudienceObject(GameObject go)
    {
        if (go == null)
        {
            return;
        }

        if (Application.isPlaying)
        {
            Destroy(go);
        }
        else
        {
            DestroyImmediate(go);
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

    private List<GameObject> FindAllSeats(Transform meetingTable, Transform meetingSeatMarkers)
    {
        if (meetingSeatMarkers != null)
        {
            List<GameObject> markerSeats = FindMeetingRoomMarkerSeats(meetingSeatMarkers);
            if (markerSeats.Count > 0)
            {
                return markerSeats;
            }
        }

        if (meetingTable != null)
        {
            List<GameObject> meetingSeats = FindMeetingRoomSeats(meetingTable);
            if (meetingSeats.Count > 0)
            {
                return meetingSeats;
            }
        }

        List<GameObject> seats = new List<GameObject>();
        GameObject[] allObjects = FindObjectsByType<GameObject>(FindObjectsSortMode.None);
        foreach (GameObject go in allObjects)
        {
            if (!IsSeatCandidate(go))
            {
                continue;
            }

            if (IsMeetingRoomSeatPart(go))
            {
                continue;
            }

            if (IsMeetingRoomPresenterSideSeat(go))
            {
                continue;
            }

            if (IsMeetingRoomSeatRoot(go))
            {
                if (!ContainsNearbySeat(seats, go.transform.position, 0.1f))
                {
                    seats.Add(go);
                }

                continue;
            }

            string n = go.name.ToLower();
            if (n.Contains("seat") || n.Contains("bench") || n.Contains("chair"))
            {
                if (!n.Contains("leg") && !n.Contains("side") && !n.Contains("back") && !n.Contains("frame"))
                {
                    Renderer r = go.GetComponent<Renderer>();
                    if (r != null && r.bounds.size.x > 1.2f) { 
                        Transform existing = go.transform.Find("VirtualSeats");
                        if (existing != null) { foreach (Transform child in existing) { RemoveSeatMarkerCollider(child.gameObject); seats.Add(child.gameObject); } }
                        else {
                            GameObject vsRoot = new GameObject("VirtualSeats"); vsRoot.transform.parent = go.transform; vsRoot.transform.localPosition = Vector3.zero;
                            float seatWidth = 0.65f; int count = Mathf.FloorToInt(r.bounds.size.x / seatWidth);
                            float startX = r.bounds.center.x - (count * seatWidth) / 2f + seatWidth / 2f;
                            for (int i = 0; i < count; i++) {
                                if (count == 3 && i == 1) continue;
                                GameObject vSeat = GameObject.CreatePrimitive(PrimitiveType.Cube); vSeat.name = "Seat_Virtual_" + i; vSeat.transform.parent = vsRoot.transform;
                                vSeat.transform.position = new Vector3(startX + i * seatWidth, r.bounds.center.y, r.bounds.center.z);
                                vSeat.transform.rotation = go.transform.rotation; vSeat.transform.localScale = new Vector3(seatWidth, r.bounds.size.y, r.bounds.size.z);
                                vSeat.GetComponent<Renderer>().enabled = false; RemoveSeatMarkerCollider(vSeat); seats.Add(vSeat);
                            }
                        }
                    }
                    else
                    {
                        if (!ContainsNearbySeat(seats, go.transform.position, 0.1f))
                        {
                            seats.Add(go);
                        }
                    }
                }
            }
        }
        return seats;
    }

    private bool IsSeatCandidate(GameObject go)
    {
        if (go == null)
        {
            return false;
        }

        string path = BuildTransformPath(go.transform);
        if (path.Contains("teacher") ||
            path.Contains("instructor") ||
            path.Contains("maindesk") ||
            path.Contains("teacherarea") ||
            path.Contains("teacher desk") ||
            path.Contains("teacherdesk") ||
            path.Contains("frontdesk") ||
            path.Contains("front desk") ||
            path.Contains("podium") ||
            path.Contains("lectern") ||
            path.Contains("whiteboard") ||
            path.Contains("blackboard") ||
            path.Contains("smartboard") ||
            path.Contains("projection") ||
            path.Contains("screen") ||
            path.Contains("presentation") ||
            path.Contains("school chair") ||
            path.Contains("desktop") ||
            path.Contains("desk_surface") ||
            path.Contains("desk_body") ||
            path.Contains("monitor"))
        {
            return false;
        }

        return true;
    }

    private Transform FindMeetingRoomSeatMarkerRoot()
    {
        GameObject[] allObjects = FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (GameObject go in allObjects)
        {
            if (go == null)
            {
                continue;
            }

            string normalizedName = go.name.Replace("_", "").Replace("-", "").Replace(" ", "").ToLowerInvariant();
            if ((normalizedName == "auidence" ||
                 normalizedName == "audience" ||
                 normalizedName == "meetingroomaudienceseats") &&
                HasNumberedSeatMarkerChildren(go.transform))
            {
                return go.transform;
            }
        }

        return null;
    }

    private bool HasNumberedSeatMarkerChildren(Transform root)
    {
        return root != null && FindMeetingRoomMarkerSeats(root).Count > 0;
    }

    private List<GameObject> FindMeetingRoomMarkerSeats(Transform markerRoot)
    {
        List<GameObject> markerSeats = new List<GameObject>();
        if (markerRoot == null)
        {
            return markerSeats;
        }

        for (int i = 0; i < markerRoot.childCount; i++)
        {
            Transform child = markerRoot.GetChild(i);
            if (child.gameObject.activeInHierarchy && GetSeatMarkerNumber(child) > 0)
            {
                markerSeats.Add(child.gameObject);
            }
        }

        markerSeats.Sort((a, b) => GetSeatMarkerNumber(a.transform).CompareTo(GetSeatMarkerNumber(b.transform)));
        return markerSeats;
    }

    private int GetSeatMarkerNumber(Transform marker)
    {
        if (marker == null)
        {
            return -1;
        }

        string name = marker.name.ToLowerInvariant();
        if (!name.StartsWith("seat"))
        {
            return -1;
        }

        int multiplier = 1;
        int value = 0;
        bool hasDigits = false;
        for (int i = name.Length - 1; i >= 0; i--)
        {
            if (name[i] < '0' || name[i] > '9')
            {
                break;
            }

            hasDigits = true;
            value += (name[i] - '0') * multiplier;
            multiplier *= 10;
        }

        return hasDigits ? value : -1;
    }

    private Transform FindMeetingRoomTable()
    {
        GameObject exact = GameObject.Find("MeetingTable_Center");
        if (exact != null)
        {
            return exact.transform;
        }

        GameObject[] allObjects = FindObjectsByType<GameObject>(FindObjectsSortMode.None);
        foreach (GameObject go in allObjects)
        {
            string normalizedName = go.name.Replace("_", "").Replace("-", "").Replace(" ", "").ToLowerInvariant();
            if (normalizedName.Contains("meetingtablecenter") ||
                (normalizedName.Contains("meeting") && normalizedName.Contains("table")))
            {
                return go.transform;
            }
        }

        Transform bestParent = null;
        int bestCount = 0;
        Dictionary<Transform, int> chairCountsByParent = new Dictionary<Transform, int>();
        foreach (GameObject go in allObjects)
        {
            Transform parent = go.transform.parent;
            if (parent == null || !LooksLikeMeetingRoomImportedChair(go.transform))
            {
                continue;
            }

            chairCountsByParent.TryGetValue(parent, out int count);
            count++;
            chairCountsByParent[parent] = count;
            if (count > bestCount)
            {
                bestCount = count;
                bestParent = parent;
            }
        }

        return bestCount >= 4 ? bestParent : null;
    }

    private List<GameObject> FindMeetingRoomSeats(Transform meetingTable)
    {
        List<GameObject> seats = new List<GameObject>();
        if (meetingTable == null)
        {
            return seats;
        }

        for (int i = 0; i < meetingTable.childCount; i++)
        {
            Transform child = meetingTable.GetChild(i);
            if (!child.gameObject.activeInHierarchy ||
                IsMeetingRoomPresenterSeat(child) ||
                !IsMeetingRoomSideSeat(child, meetingTable) ||
                !LooksLikeMeetingRoomChair(child))
            {
                continue;
            }

            seats.Add(child.gameObject);
        }

        return seats;
    }

    private bool LooksLikeMeetingRoomChair(Transform candidate)
    {
        if (candidate == null)
        {
            return false;
        }

        string name = candidate.name.ToLowerInvariant();
        return name.Contains("chair") ||
               name.Contains("zero7") ||
               name.Contains("[900486]") ||
               HasDescendantName(candidate, "zero7");
    }

    private bool LooksLikeMeetingRoomImportedChair(Transform candidate)
    {
        if (candidate == null)
        {
            return false;
        }

        string name = candidate.name.ToLowerInvariant();
        return name.Contains("zero7") ||
               name.Contains("[900486]") ||
               HasDescendantName(candidate, "zero7");
    }

    private bool IsMeetingRoomPresenterSeat(Transform seat)
    {
        return seat != null && seat.localPosition.z < -7f;
    }

    private bool IsMeetingRoomSideSeat(Transform seat, Transform meetingTable)
    {
        if (seat == null || meetingTable == null)
        {
            return false;
        }

        if (HasMeetingRoomSideName(seat, "left") || HasMeetingRoomSideName(seat, "right"))
        {
            return true;
        }

        Vector3 localSeatPosition = meetingTable.InverseTransformPoint(seat.position);
        return Mathf.Abs(localSeatPosition.z) >= meetingRoomMinimumSideDistance;
    }

    private bool HasMeetingRoomSideName(Transform seat, string side)
    {
        if (seat == null)
        {
            return false;
        }

        string name = seat.name.ToLowerInvariant();
        return name.EndsWith(side) ||
               name.Contains("_" + side) ||
               name.Contains("-" + side) ||
               name.Contains(" " + side);
    }

    private bool IsMeetingRoomSeatRoot(GameObject go)
    {
        if (go == null)
        {
            return false;
        }

        Transform parent = go.transform.parent;
        if (parent == null || !parent.name.ToLowerInvariant().Contains("meetingtable_center"))
        {
            return false;
        }

        string name = go.name.ToLowerInvariant();
        return name.Contains("zero7") ||
               name.Contains("[900486]") ||
               HasDescendantName(go.transform, "zero7");
    }

    private bool IsMeetingRoomPresenterSideSeat(GameObject go)
    {
        if (!IsMeetingRoomSeatRoot(go))
        {
            return false;
        }

        return go.transform.localPosition.z < -7f;
    }

    private bool IsMeetingRoomSeatPart(GameObject go)
    {
        if (go == null)
        {
            return false;
        }

        string path = BuildTransformPath(go.transform);
        return path.Contains("meetingtable_center") &&
               path.Contains("zero7") &&
               (path.Contains("geometry_") || path.Contains("_navisworks"));
    }

    private bool ContainsNearbySeat(List<GameObject> seats, Vector3 position, float distance)
    {
        foreach (GameObject seat in seats)
        {
            if (seat != null && Vector3.Distance(seat.transform.position, position) < distance)
            {
                return true;
            }
        }

        return false;
    }

    private Transform EnsureMeetingRoomLookTarget(Transform meetingTable, Vector3 fallbackFocalPoint)
    {
        const string targetName = "AudienceLookTarget_MeetingRoom";
        GameObject target = GameObject.Find(targetName);

        if (target == null)
        {
            target = new GameObject(targetName);
        }

        if (meetingTable != null)
        {
            target.transform.SetParent(meetingTable, false);
            target.transform.localPosition = new Vector3(0f, 0.75f, 0f);
            target.transform.localRotation = Quaternion.identity;
            target.transform.localScale = Vector3.one;
        }
        else
        {
            target.transform.SetParent(null);
            target.transform.position = fallbackFocalPoint;
            target.transform.rotation = Quaternion.identity;
            target.transform.localScale = Vector3.one;
        }

        return target.transform;
    }

    private Dictionary<GameObject, Vector3> BuildMeetingRoomMarkerFacingDirections(List<GameObject> seats)
    {
        Dictionary<GameObject, Vector3> directions = new Dictionary<GameObject, Vector3>();
        if (seats == null || seats.Count == 0)
        {
            return directions;
        }

        List<GameObject> rightSide = new List<GameObject>();
        List<GameObject> leftSide = new List<GameObject>();
        foreach (GameObject seat in seats)
        {
            if (seat == null)
            {
                continue;
            }

            int seatNumber = GetSeatMarkerNumber(seat.transform);
            if (seatNumber >= 1 && seatNumber <= 3)
            {
                rightSide.Add(seat);
            }
            else
            {
                leftSide.Add(seat);
            }
        }

        if (rightSide.Count > 0 && leftSide.Count > 0)
        {
            Vector3 rightCenter = GetAverageSeatPosition(rightSide);
            Vector3 leftCenter = GetAverageSeatPosition(leftSide);
            AddDirectionsTowardCenter(directions, rightSide, leftCenter);
            AddDirectionsTowardCenter(directions, leftSide, rightCenter);
        }

        foreach (GameObject seat in seats)
        {
            if (seat != null && !directions.ContainsKey(seat))
            {
                Vector3 direction = seat.transform.forward;
                direction.y = 0f;
                directions[seat] = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector3.forward;
            }
        }

        return directions;
    }

    private Dictionary<GameObject, Vector3> BuildMeetingRoomFacingDirections(List<GameObject> seats, Transform meetingTable, Transform fallbackTarget)
    {
        Dictionary<GameObject, Vector3> directions = new Dictionary<GameObject, Vector3>();
        if (seats == null || seats.Count == 0 || meetingTable == null)
        {
            return directions;
        }

        List<GameObject> sideA = new List<GameObject>();
        List<GameObject> sideB = new List<GameObject>();
        foreach (GameObject seat in seats)
        {
            if (seat == null)
            {
                continue;
            }

            if (HasMeetingRoomSideName(seat.transform, "left"))
            {
                sideA.Add(seat);
            }
            else if (HasMeetingRoomSideName(seat.transform, "right"))
            {
                sideB.Add(seat);
            }
        }

        if (sideA.Count > 0 && sideB.Count == 0)
        {
            foreach (GameObject seat in seats)
            {
                if (seat != null && !sideA.Contains(seat))
                {
                    sideB.Add(seat);
                }
            }
        }
        else if (sideB.Count > 0 && sideA.Count == 0)
        {
            foreach (GameObject seat in seats)
            {
                if (seat != null && !sideB.Contains(seat))
                {
                    sideA.Add(seat);
                }
            }
        }

        if (sideA.Count == 0 || sideB.Count == 0)
        {
            sideA.Clear();
            sideB.Clear();
            foreach (GameObject seat in seats)
            {
                if (seat == null)
                {
                    continue;
                }

                Vector3 localSeatPosition = meetingTable.InverseTransformPoint(seat.transform.position);
                if (localSeatPosition.z >= 0f)
                {
                    sideA.Add(seat);
                }
                else
                {
                    sideB.Add(seat);
                }
            }
        }

        if (sideA.Count > 0 && sideB.Count > 0)
        {
            Vector3 sideACenter = GetAverageSeatPosition(sideA);
            Vector3 sideBCenter = GetAverageSeatPosition(sideB);
            AddDirectionsTowardCenter(directions, sideA, sideBCenter);
            AddDirectionsTowardCenter(directions, sideB, sideACenter);
        }

        foreach (GameObject seat in seats)
        {
            if (seat != null && !directions.ContainsKey(seat))
            {
                directions[seat] = GetMeetingRoomSeatFacingDirection(seat, meetingTable, fallbackTarget, null);
            }
        }

        return directions;
    }

    private Vector3 GetAverageSeatPosition(List<GameObject> seats)
    {
        Vector3 sum = Vector3.zero;
        int count = 0;
        foreach (GameObject seat in seats)
        {
            if (seat == null)
            {
                continue;
            }

            sum += seat.transform.position;
            count++;
        }

        return count > 0 ? sum / count : Vector3.zero;
    }

    private void AddDirectionsTowardCenter(Dictionary<GameObject, Vector3> directions, List<GameObject> seats, Vector3 targetCenter)
    {
        foreach (GameObject seat in seats)
        {
            if (seat == null)
            {
                continue;
            }

            Vector3 direction = targetCenter - seat.transform.position;
            direction.y = 0f;
            if (direction.sqrMagnitude > 0.0001f)
            {
                directions[seat] = direction.normalized;
            }
        }
    }

    private Vector3 GetMeetingRoomSeatFacingDirection(
        GameObject seat,
        Transform meetingTable,
        Transform fallbackTarget,
        Dictionary<GameObject, Vector3> groupedDirections)
    {
        if (seat != null && groupedDirections != null &&
            groupedDirections.TryGetValue(seat, out Vector3 groupedDirection) &&
            groupedDirection.sqrMagnitude > 0.0001f)
        {
            return groupedDirection.normalized;
        }

        if (seat != null && meetingTable != null)
        {
            Vector3 localSeatPosition = meetingTable.InverseTransformPoint(seat.transform.position);
            Vector3 localTargetPosition = localSeatPosition;

            if (Mathf.Abs(localSeatPosition.z) > 0.5f)
            {
                localTargetPosition.z = 0f;
            }
            else
            {
                localTargetPosition.x = 0f;
            }

            Vector3 direction = meetingTable.TransformPoint(localTargetPosition) - seat.transform.position;
            direction.y = 0f;
            if (direction.sqrMagnitude > 0.0001f)
            {
                return direction.normalized;
            }
        }

        if (seat != null && fallbackTarget != null)
        {
            Vector3 fallbackDirection = fallbackTarget.position - seat.transform.position;
            fallbackDirection.y = 0f;
            if (fallbackDirection.sqrMagnitude > 0.0001f)
            {
                return fallbackDirection.normalized;
            }
        }

        return Vector3.forward;
    }

    private void AttachMeetingRoomFacing(GameObject memberObj, Vector3 facingDirection)
    {
        if (memberObj == null || facingDirection.sqrMagnitude < 0.0001f)
        {
            return;
        }

        AudienceFaceTarget faceTarget = memberObj.GetComponent<AudienceFaceTarget>();
        if (faceTarget == null)
        {
            faceTarget = memberObj.AddComponent<AudienceFaceTarget>();
        }

        faceTarget.Configure(facingDirection, meetingRoomVisualYawOffset, drawMeetingRoomFacingDebug);
    }

    private bool HasDescendantName(Transform root, string token)
    {
        if (root == null)
        {
            return false;
        }

        string normalizedToken = token.ToLowerInvariant();
        for (int i = 0; i < root.childCount; i++)
        {
            Transform child = root.GetChild(i);
            if (child.name.ToLowerInvariant().Contains(normalizedToken) ||
                HasDescendantName(child, normalizedToken))
            {
                return true;
            }
        }

        return false;
    }

    private string BuildTransformPath(Transform transform)
    {
        System.Text.StringBuilder builder = new System.Text.StringBuilder();
        for (Transform current = transform; current != null; current = current.parent)
        {
            if (builder.Length > 0)
            {
                builder.Append('/');
            }

            builder.Append(current.name.ToLowerInvariant());
        }

        return builder.ToString();
    }

    private void RemoveSeatMarkerCollider(GameObject seatMarker)
    {
        Collider collider = seatMarker != null ? seatMarker.GetComponent<Collider>() : null;
        if (collider != null)
        {
            Destroy(collider);
        }
    }

    private List<GameObject> LoadCharacterPrefabs()
    {
        List<GameObject> prefabs = new List<GameObject>();
        if (audiencePrefabs != null)
        {
            foreach (GameObject prefab in audiencePrefabs)
            {
                if (prefab != null && !prefabs.Contains(prefab))
                {
                    prefabs.Add(prefab);
                }
            }
        }

#if UNITY_EDITOR
        string folder = "Assets/AudienceSimulation_Arda/Models";
        if (System.IO.Directory.Exists(Application.dataPath + "/" + folder.Replace("Assets/", "")))
        {
            string[] guids = AssetDatabase.FindAssets("t:Model t:Prefab", new[] { folder });
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                string fileName = System.IO.Path.GetFileName(path).ToLower();
                if (fileName.Contains("@") || fileName.Contains("material") || fileName.Contains("anim") || fileName.Contains("sitting")) continue;
                GameObject go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (go != null && go.GetComponentInChildren<SkinnedMeshRenderer>() != null) { if (!prefabs.Contains(go)) prefabs.Add(go); }
            }
        }
#endif
        return prefabs;
    }

    [ContextMenu("Force Spawn Now")]
    public void ForceSpawn() { SpawnAudience(); }

    [ContextMenu("Auto Find All Prefabs (Click before Build)")]
    public void AutoFindPrefabs()
    {
#if UNITY_EDITOR
        if (audiencePrefabs == null) audiencePrefabs = new List<GameObject>();
        audiencePrefabs.Clear();
        string folder = "Assets/AudienceSimulation_Arda/Models";
        if (System.IO.Directory.Exists(Application.dataPath + "/" + folder.Replace("Assets/", "")))
        {
            string[] guids = UnityEditor.AssetDatabase.FindAssets("t:Model t:Prefab", new[] { folder });
            foreach (string guid in guids)
            {
                string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
                string fileName = System.IO.Path.GetFileName(path).ToLower();
                if (fileName.Contains("@") || fileName.Contains("material") || fileName.Contains("anim") || fileName.Contains("sitting")) continue;
                GameObject go = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (go != null && go.GetComponentInChildren<SkinnedMeshRenderer>() != null) { if (!audiencePrefabs.Contains(go)) audiencePrefabs.Add(go); }
            }
            UnityEditor.EditorUtility.SetDirty(this);
            Debug.Log($"[AudienceSpawner] Başarıyla {audiencePrefabs.Count} adet karakter bulundu ve listeye eklendi. Lütfen sahneyi kaydedin (Ctrl+S)!");
        }
#endif
    }

    private void ApplyRandomPersonality(AudienceMember am)
    {
        switch (controller.currentStressLevel)
        {
            case StressLevel.Easy:
                am.personalWpmTolerance = Random.Range(10f, 40f);
                am.personalEyeContactTolerance = Random.Range(0.08f, 0.20f);
                break;
            case StressLevel.Medium:
                am.personalWpmTolerance = Random.Range(-10f, 10f);
                am.personalEyeContactTolerance = Random.Range(-0.08f, 0.08f);
                break;
            case StressLevel.Hard:
                am.personalWpmTolerance = Random.Range(-40f, -10f);
                am.personalEyeContactTolerance = Random.Range(-0.22f, -0.08f);
                break;
        }
    }

    private int GetAudienceCount()
    {
        Transform markerRoot = FindMeetingRoomSeatMarkerRoot();
        if (markerRoot != null)
        {
            int markerCount = FindMeetingRoomMarkerSeats(markerRoot).Count;
            if (markerCount > 0) return markerCount;
        }

        if (FindMeetingRoomTable() != null) return 8;

        switch (controller.currentStressLevel)
        {
            case StressLevel.Easy: return 12;
            case StressLevel.Medium: return 30;
            case StressLevel.Hard: return 60;
            default: return 30;
        }
    }

    private Vector3 GetSeatBackwardOffset(GameObject seat)
    {
        Vector3 b = seat.transform.forward;
        b.y = 0f;
        if (b.sqrMagnitude < 0.0001f)
        {
            return Vector3.zero;
        }

        b.Normalize();
        return b * seatBackOffset;
    }

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
        if (controller == null)
        {
            controller = FindFirstObjectByType<AudienceBehaviorController>(FindObjectsInactive.Include);
        }

        if (controller == null)
        {
            controller = gameObject.AddComponent<AudienceBehaviorController>();
        }

#if UNITY_EDITOR
        if (sharedAnimatorController == null) {
            string[] guids = AssetDatabase.FindAssets("AudienceAnimator t:RuntimeAnimatorController");
            if (guids.Length > 0) sharedAnimatorController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(AssetDatabase.GUIDToAssetPath(guids[0]));
        }
#endif
    }
}

[DefaultExecutionOrder(200)]
public class AudienceFaceTarget : MonoBehaviour
{
    [SerializeField] private Transform target;
    [SerializeField] private bool useFixedDirection;
    [SerializeField] private Vector3 fixedWorldDirection = Vector3.forward;
    [SerializeField] private float yawOffsetDegrees;
    [SerializeField] private bool drawDebugLine;
    [SerializeField] private float minDistance = 0.05f;

    public void Configure(Transform lookTarget, float visualYawOffsetDegrees, bool debugLine = false)
    {
        target = lookTarget;
        useFixedDirection = false;
        yawOffsetDegrees = visualYawOffsetDegrees;
        drawDebugLine = debugLine;
        enabled = target != null;
        ApplyNow();
    }

    public void Configure(Vector3 worldDirection, float visualYawOffsetDegrees, bool debugLine = false)
    {
        worldDirection.y = 0f;
        if (worldDirection.sqrMagnitude < minDistance * minDistance)
        {
            enabled = false;
            return;
        }

        target = null;
        useFixedDirection = true;
        fixedWorldDirection = worldDirection.normalized;
        yawOffsetDegrees = visualYawOffsetDegrees;
        drawDebugLine = debugLine;
        enabled = true;
        ApplyNow();
    }

    private void LateUpdate()
    {
        ApplyNow();
    }

    public void ApplyNow()
    {
        Vector3 direction;
        if (useFixedDirection)
        {
            direction = fixedWorldDirection;
        }
        else if (target != null)
        {
            direction = target.position - transform.position;
        }
        else
        {
            return;
        }

        direction.y = 0f;
        if (direction.sqrMagnitude < minDistance * minDistance)
        {
            return;
        }

        Quaternion lookRotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
        transform.rotation = lookRotation * Quaternion.Euler(0f, yawOffsetDegrees, 0f);

        if (drawDebugLine)
        {
            Debug.DrawLine(transform.position + Vector3.up, transform.position + Vector3.up + direction.normalized * 1.5f, Color.cyan, 0f, false);
        }
    }
}
