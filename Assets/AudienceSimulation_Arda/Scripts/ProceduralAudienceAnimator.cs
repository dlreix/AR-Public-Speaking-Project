using UnityEngine;
using System.Collections;

public class ProceduralAudienceAnimator : MonoBehaviour
{
    public enum StudentState { Idle, Nodding, Distracted, Stretching, Applauding, NoteTaking, ChinResting }

    [Header("State Control")]
    [SerializeField] private StudentState currentState = StudentState.Idle;

    [Header("Bone References")]
    [SerializeField] private Transform hips;
    [SerializeField] private Transform spine;
    [SerializeField] private Transform neck; 
    [SerializeField] private Transform head;
    
    [SerializeField] private Transform rightUpperArm, rightLowerArm, rightHand;
    [SerializeField] private Transform leftUpperArm, leftLowerArm, leftHand;
    
    [SerializeField] private Transform leftUpperLeg, leftLowerLeg;
    [SerializeField] private Transform rightUpperLeg, rightLowerLeg;

    [Header("Settings")]
    [SerializeField] private float transitionSpeed = 4f;
    [SerializeField] private float breathingSpeed = 2f;
    [SerializeField] private float breathingAmount = 6f; 
    [SerializeField] private float noddingSpeed = 5f;
    [SerializeField] private float noddingAmount = 25f; 
    [SerializeField] private float distractionSpeed = 1.5f;
    [SerializeField] private float distractionAmount = 35f;

    [Header("External Integration")]
    [HideInInspector] public float externalBoredomLevel = 0f;
    [HideInInspector] public float externalPerformanceScore = 0f;
    [HideInInspector] public Transform[] distractionTargets;

    private Quaternion initialSpineRot, initialNeckRot, initialHeadRot;
    private Quaternion initialRightUpperArmRot, initialRightLowerArmRot, initialRightHandRot;
    private Quaternion initialLeftUpperArmRot, initialLeftLowerArmRot, initialLeftHandRot;

    // Forces lower body to remain seated
    private Vector3 initialHipsPos;
    private Quaternion initialHipsRot;
    private Quaternion initialLeftUpperLegRot, initialLeftLowerLegRot;
    private Quaternion initialRightUpperLegRot, initialRightLowerLegRot;

    private Quaternion targetSpineRot, targetNeckRot, targetHeadRot;
    private Quaternion currentSpineRot, currentNeckRot, currentHeadRot;

    private Quaternion current_rUA_rot, current_rLA_rot, current_rH_rot;
    private Quaternion current_lUA_rot, current_lLA_rot, current_lH_rot;

    private float timer;
    private Animator _animator;
    private float m_currentLayerWeight = 0f; 
    private float m_boredomCycleTimer = 0f;  

    void Awake()
    {
        _animator = GetComponentInChildren<Animator>();

        if (spine == null || head == null)
        {
            if (_animator != null && _animator.isHuman)
            {
                if (spine == null) spine = _animator.GetBoneTransform(HumanBodyBones.Spine);
                if (neck == null)  neck  = _animator.GetBoneTransform(HumanBodyBones.Neck);
                if (head == null)  head  = _animator.GetBoneTransform(HumanBodyBones.Head);
                
                if (rightUpperArm == null) rightUpperArm = _animator.GetBoneTransform(HumanBodyBones.RightUpperArm);
                if (rightLowerArm == null) rightLowerArm = _animator.GetBoneTransform(HumanBodyBones.RightLowerArm);
                if (rightHand == null)     rightHand     = _animator.GetBoneTransform(HumanBodyBones.RightHand);
                
                if (leftUpperArm == null)  leftUpperArm  = _animator.GetBoneTransform(HumanBodyBones.LeftUpperArm);
                if (leftLowerArm == null)  leftLowerArm  = _animator.GetBoneTransform(HumanBodyBones.LeftLowerArm);
                if (leftHand == null)      leftHand      = _animator.GetBoneTransform(HumanBodyBones.LeftHand);

                if (hips == null)          hips          = _animator.GetBoneTransform(HumanBodyBones.Hips);
                if (leftUpperLeg == null)  leftUpperLeg  = _animator.GetBoneTransform(HumanBodyBones.LeftUpperLeg);
                if (leftLowerLeg == null)  leftLowerLeg  = _animator.GetBoneTransform(HumanBodyBones.LeftLowerLeg);
                if (rightUpperLeg == null) rightUpperLeg = _animator.GetBoneTransform(HumanBodyBones.RightUpperLeg);
                if (rightLowerLeg == null) rightLowerLeg = _animator.GetBoneTransform(HumanBodyBones.RightLowerLeg);
                }

                if (spine == null) spine = FindBoneRecursive(transform, "Spine");
                if (neck == null)  neck  = FindBoneRecursive(transform, "Neck");
                if (head == null)  head  = FindBoneRecursive(transform, "Head");
                if (rightUpperArm == null) rightUpperArm = FindBoneRecursive(transform, "RightArm");
                if (rightLowerArm == null) rightLowerArm = FindBoneRecursive(transform, "RightForeArm");
                if (rightHand == null) rightHand = FindBoneRecursive(transform, "RightHand");
                if (hips == null) hips = FindBoneRecursive(transform, "Hips");
                if (leftUpperLeg == null) leftUpperLeg = FindBoneRecursive(transform, "LeftUpLeg");
                if (leftLowerLeg == null) leftLowerLeg = FindBoneRecursive(transform, "LeftLeg");
                if (rightUpperLeg == null) rightUpperLeg = FindBoneRecursive(transform, "RightUpLeg");
                if (rightLowerLeg == null) rightLowerLeg = FindBoneRecursive(transform, "RightLeg");

                if (spine == null || head == null)            {
                Debug.LogError($"[ProceduralAudienceAnimator] {gameObject.name} karakterinde Spine veya Head kemiği bulunamadı!", this);
            }
        }
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

    void Start()
    {
        if (spine != null) { initialSpineRot = spine.localRotation; currentSpineRot = initialSpineRot; }
        if (neck != null) { initialNeckRot = neck.localRotation; currentNeckRot = initialNeckRot; }
        if (head != null) { initialHeadRot = head.localRotation; currentHeadRot = initialHeadRot; }

        if (rightUpperArm != null) { initialRightUpperArmRot = rightUpperArm.localRotation; current_rUA_rot = initialRightUpperArmRot; }
        if (rightLowerArm != null) { initialRightLowerArmRot = rightLowerArm.localRotation; current_rLA_rot = initialRightLowerArmRot; }
        if (rightHand != null) { initialRightHandRot = rightHand.localRotation; current_rH_rot = initialRightHandRot; }

        if (leftUpperArm != null) { initialLeftUpperArmRot = leftUpperArm.localRotation; current_lUA_rot = initialLeftUpperArmRot; }
        if (leftLowerArm != null) { initialLeftLowerArmRot = leftLowerArm.localRotation; current_lLA_rot = initialLeftLowerArmRot; }
        if (leftHand != null) { initialLeftHandRot = leftHand.localRotation; current_lH_rot = initialLeftHandRot; }

        if (hips != null) { initialHipsPos = hips.localPosition; initialHipsRot = hips.localRotation; }
        if (leftUpperLeg != null) initialLeftUpperLegRot = leftUpperLeg.localRotation;
        if (leftLowerLeg != null) initialLeftLowerLegRot = leftLowerLeg.localRotation;
        if (rightUpperLeg != null) initialRightUpperLegRot = rightUpperLeg.localRotation;
        if (rightLowerLeg != null) initialRightLowerLegRot = rightLowerLeg.localRotation;

        timer = Random.Range(0f, 1000f);
    }

    void LateUpdate()
    {
        if (spine == null || head == null) return;

        if (currentState == StudentState.Applauding)
        {
            if (_animator != null && _animator.layerCount > 1) 
            {
                m_currentLayerWeight = Mathf.MoveTowards(m_currentLayerWeight, 0f, Time.deltaTime * 3f);
                _animator.SetLayerWeight(1, m_currentLayerWeight);
            }
            
            if (hips != null) { hips.localPosition = initialHipsPos; hips.localRotation = initialHipsRot; }
            if (leftUpperLeg != null) leftUpperLeg.localRotation = initialLeftUpperLegRot;
            if (leftLowerLeg != null) leftLowerLeg.localRotation = initialLeftLowerLegRot;
            if (rightUpperLeg != null) rightUpperLeg.localRotation = initialRightUpperLegRot;
            if (rightLowerLeg != null) rightLowerLeg.localRotation = initialRightLowerLegRot;

            return;
        }

        timer += Time.deltaTime;

        Quaternion animSpine = spine.localRotation;
        Quaternion animNeck  = neck != null ? neck.localRotation : Quaternion.identity;
        Quaternion animHead  = head.localRotation;

        Quaternion target_rUA_rot = rightUpperArm != null ? initialRightUpperArmRot : Quaternion.identity;
        Quaternion target_rLA_rot = rightLowerArm != null ? initialRightLowerArmRot : Quaternion.identity;
        Quaternion target_rH_rot  = rightHand != null ? initialRightHandRot : Quaternion.identity;

        Quaternion target_lUA_rot = leftUpperArm != null ? initialLeftUpperArmRot : Quaternion.identity;
        Quaternion target_lLA_rot = leftLowerArm != null ? initialLeftLowerArmRot : Quaternion.identity;
        Quaternion target_lH_rot  = leftHand != null ? initialLeftHandRot : Quaternion.identity;

        float breath = Mathf.Sin(timer * breathingSpeed) * (breathingAmount * (1f + (Mathf.PerlinNoise(timer * 0.1f, 0f) - 0.5f) * 0.3f));
        float microSwayX = (Mathf.PerlinNoise(timer * 0.05f, 10f) - 0.5f) * 2f; 
        float microSwayZ = (Mathf.PerlinNoise(20f, timer * 0.05f) - 0.5f) * 1.5f;
        Quaternion breathOffset = SafeEuler(breath + microSwayX, 0f, microSwayZ, spine);

        bool isStateMocap = (currentState == StudentState.Stretching || 
                             currentState == StudentState.Distracted || 
                             currentState == StudentState.Nodding || 
                             currentState == StudentState.NoteTaking ||
                             currentState == StudentState.ChinResting);
        
        bool isPlayingEmpty = _animator != null && _animator.layerCount > 1 && 
                              _animator.GetCurrentAnimatorStateInfo(1).IsName("Empty") && 
                              !_animator.IsInTransition(1);

        // MoCap is always considered active if the state demands it
        bool isMocapActive = isStateMocap;

        if (isStateMocap && currentState == StudentState.Distracted)
        {
            m_boredomCycleTimer += Time.deltaTime;
            if (m_boredomCycleTimer > Random.Range(7f, 12f)) 
            {
                m_boredomCycleTimer = 0;
                TriggerRandomDistraction();
            }
        }
        else { m_boredomCycleTimer = 0; }

        // If we are just sitting in the 'Empty' state waiting, we should fade out the layer weight
        // so that the Base Layer (idle sway, breathing) can take over.
        float targetWeight = (isMocapActive && !isPlayingEmpty) ? 1.0f : 0.0f;
        m_currentLayerWeight = Mathf.MoveTowards(m_currentLayerWeight, targetWeight, Time.deltaTime * 3f);
        
        if (_animator != null && _animator.layerCount > 1) _animator.SetLayerWeight(1, m_currentLayerWeight);

        if (isMocapActive && targetWeight > 0.0f)
        {
            // MOCAP AKTİF: Animator'den gelen veriyi "Hedef" (Target) olarak alıyoruz 
            // ama bu karede kemiklere dokunmuyoruz (Animator'ü ezmemek için).
            targetSpineRot = animSpine;
            if (neck != null) targetNeckRot = animNeck;
            targetHeadRot = animHead;

            target_rUA_rot = rightUpperArm != null ? rightUpperArm.localRotation : Quaternion.identity;
            target_rLA_rot = rightLowerArm != null ? rightLowerArm.localRotation : Quaternion.identity;
            target_rH_rot  = rightHand != null ? rightHand.localRotation : Quaternion.identity;

            target_lUA_rot = leftUpperArm != null ? leftUpperArm.localRotation : Quaternion.identity;
            target_lLA_rot = leftLowerArm != null ? leftLowerArm.localRotation : Quaternion.identity;
            target_lH_rot  = leftHand != null ? leftHand.localRotation : Quaternion.identity;
        }
        else
        {
            CalculateTargets(animSpine, animNeck, animHead, breathOffset, breath,
                             ref target_rUA_rot, ref target_rLA_rot, ref target_rH_rot, 
                             ref target_lUA_rot, ref target_lLA_rot, ref target_lH_rot);
        }

        float speed = isMocapActive ? transitionSpeed * 12f : transitionSpeed;
        
        // --- KRİTİK FİX: MOCAP KATMANI AKTİFKEN KEMİKLERE YAZMA! (Zombi Donmasını Engeller) ---
        // Eğer geçiş ağırlığı %80'i geçtiyse Animator'e tam hakimiyet veriyoruz.
        bool animatorSovereignty = (targetWeight > 0.0f && m_currentLayerWeight > 0.8f);

        currentSpineRot = Quaternion.Slerp(currentSpineRot, targetSpineRot, Time.deltaTime * speed);
        if (neck != null) currentNeckRot = Quaternion.Slerp(currentNeckRot, targetNeckRot, Time.deltaTime * speed);
        currentHeadRot = Quaternion.Slerp(currentHeadRot, targetHeadRot, Time.deltaTime * speed);

        if (!animatorSovereignty)
        {
            spine.localRotation = currentSpineRot;
            if (neck != null) neck.localRotation = currentNeckRot;
            head.localRotation = currentHeadRot;
        }

        if (rightUpperArm != null) { current_rUA_rot = Quaternion.Slerp(current_rUA_rot, target_rUA_rot, Time.deltaTime * speed); if (!animatorSovereignty) rightUpperArm.localRotation = current_rUA_rot; }
        if (rightLowerArm != null) { current_rLA_rot = Quaternion.Slerp(current_rLA_rot, target_rLA_rot, Time.deltaTime * speed); if (!animatorSovereignty) rightLowerArm.localRotation = current_rLA_rot; }
        if (rightHand != null) { current_rH_rot = Quaternion.Slerp(current_rH_rot, target_rH_rot, Time.deltaTime * speed); if (!animatorSovereignty) rightHand.localRotation = current_rH_rot; }

        if (leftUpperArm != null) { current_lUA_rot = Quaternion.Slerp(current_lUA_rot, target_lUA_rot, Time.deltaTime * speed); if (!animatorSovereignty) leftUpperArm.localRotation = current_lUA_rot; }
        if (leftLowerArm != null) { current_lLA_rot = Quaternion.Slerp(current_lLA_rot, target_lLA_rot, Time.deltaTime * speed); if (!animatorSovereignty) leftLowerArm.localRotation = current_lLA_rot; }
        if (leftHand != null) { current_lH_rot = Quaternion.Slerp(current_lH_rot, target_lH_rot, Time.deltaTime * speed); if (!animatorSovereignty) leftHand.localRotation = current_lH_rot; }

        // --- ENFORCE SITTING POSTURE ---
        // Prevents standing animations from moving the lower body
        if (hips != null) { hips.localPosition = initialHipsPos; hips.localRotation = initialHipsRot; }
        if (leftUpperLeg != null) leftUpperLeg.localRotation = initialLeftUpperLegRot;
        if (leftLowerLeg != null) leftLowerLeg.localRotation = initialLeftLowerLegRot;
        if (rightUpperLeg != null) rightUpperLeg.localRotation = initialRightUpperLegRot;
        if (rightLowerLeg != null) rightLowerLeg.localRotation = initialRightLowerLegRot;
    }

    private Quaternion SafeEuler(float pF, float yR, float rS, Transform bone)
    {
        if (bone == null || bone.parent == null) return Quaternion.identity;
        Vector3 r = bone.parent.InverseTransformDirection(transform.right);
        Vector3 u = bone.parent.InverseTransformDirection(transform.up);
        Vector3 f = bone.parent.InverseTransformDirection(transform.forward);
        return Quaternion.AngleAxis(pF, r) * Quaternion.AngleAxis(yR, u) * Quaternion.AngleAxis(rS, f);
    }

    private void CalculateTargets(Quaternion aS, Quaternion aN, Quaternion aH, Quaternion bO, float b,
                                  ref Quaternion tRUA, ref Quaternion tRLA, ref Quaternion tRH, 
                                  ref Quaternion tLUA, ref Quaternion tLLA, ref Quaternion tLH)
    {
        Quaternion nO = SafeEuler(b * -0.1f, 0f, 0f, neck);
        Quaternion hO = SafeEuler(b * -0.2f, 0f, 0f, head);

        switch (currentState)
        {
            case StudentState.Idle:
                targetSpineRot = bO * aS;
                if (neck != null) targetNeckRot = nO * aN;
                targetHeadRot = hO * aH;
                break;
            case StudentState.NoteTaking:
                targetSpineRot = SafeEuler(12f, 0f, 0f, spine) * bO * aS;
                if (neck != null) targetNeckRot = SafeEuler(15f, 0f, 0f, neck) * nO * aN;
                targetHeadRot = SafeEuler(20f, 0f, 0f, head) * hO * aH;
                tRUA = SafeEuler(25f, -10f, -15f, rightUpperArm) * initialRightUpperArmRot;
                tRLA = SafeEuler(75f, 25f, 0f, rightLowerArm) * initialRightLowerArmRot;
                tLUA = SafeEuler(25f, -15f, 0f, leftUpperArm) * initialLeftUpperArmRot;
                tLLA = SafeEuler(65f, 15f, 0f, leftLowerArm) * initialLeftLowerArmRot;
                break;
            case StudentState.Nodding:
                targetSpineRot = bO * aS;
                float nod = Mathf.Sin(timer * noddingSpeed) * noddingAmount;
                if (neck != null) targetNeckRot = SafeEuler(nod * 0.4f, 0f, 0f, neck) * aN;
                targetHeadRot = SafeEuler(nod * 0.6f, 0f, 0f, head) * aH;
                break;
            case StudentState.ChinResting:
                targetSpineRot = SafeEuler(20f, -10f, 5f, spine) * bO * aS;
                targetHeadRot = SafeEuler(-15f, 5f, -10f, head) * aH;
                tLUA = SafeEuler(20f, 25f, 20f, leftUpperArm) * initialLeftUpperArmRot;
                tLLA = SafeEuler(110f, 10f, 0f, leftLowerArm) * initialLeftLowerArmRot;
                break;
            case StudentState.Distracted:
                float swX = (Mathf.PerlinNoise(timer * 0.6f, 0f) - 0.5f) * 15f;
                targetSpineRot = SafeEuler(swX, 0f, 0f, spine) * bO * aS;
                float lX = (Mathf.PerlinNoise(timer * distractionSpeed, 0f) - 0.5f) * distractionAmount;
                float lY = (Mathf.PerlinNoise(0f, timer * distractionSpeed) - 0.5f) * distractionAmount;
                if (neck != null) targetNeckRot = SafeEuler(lX * 0.4f, lY * 0.4f, 0f, neck) * aN;
                targetHeadRot = SafeEuler(lX * 0.6f, lY * 0.6f, 0f, head) * aH;
                break;
            case StudentState.Stretching:
                targetSpineRot = bO * aS;
                break;
        }
    }

    public void SetState(AudienceState extState)
    {
        if (_animator == null) _animator = GetComponentInChildren<Animator>();
        Debug.Log($"<b>[AUDIENCE]</b> {gameObject.name} state changed to: {extState}");

        switch (extState)
        {
            case AudienceState.Applauding:
                currentState = StudentState.Applauding;
                if (_animator != null) _animator.SetTrigger("Applaud"); 
                break;
            case AudienceState.Nodding:
            case AudienceState.Attentive:
                currentState = StudentState.Nodding;
                if (_animator != null) _animator.SetTrigger("Attentive");
                break;
            case AudienceState.NoteTaking:
                currentState = StudentState.NoteTaking;
                if (_animator != null) _animator.SetTrigger("Writing");
                break;
            case AudienceState.Distracted:
                currentState = StudentState.Distracted;
                TriggerRandomDistraction();
                break;
            case AudienceState.Stretching:
                currentState = StudentState.Stretching;
                if (_animator != null) _animator.SetTrigger("Yawn");
                break;
            default:
                currentState = StudentState.Idle;
                break;
        }
    }

    private void TriggerRandomDistraction()
    {
        if (_animator == null) return;
        float r = Random.value;
        if (r > 0.65f) _animator.SetTrigger("Texting");
        else if (r > 0.35f) _animator.SetTrigger("Tablet");
        else _animator.SetTrigger("Distracted"); // Look Around
    }
}
