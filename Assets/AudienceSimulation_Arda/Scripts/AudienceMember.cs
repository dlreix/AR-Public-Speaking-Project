using System.Collections;
using UnityEngine;

public class AudienceMember : MonoBehaviour
{
    public Animator animator;
    public ProceduralAudienceAnimator proceduralAnimator;
    public float reactionDelay = 0f;
    public float personalWpmTolerance;
    public float personalEyeContactTolerance;
    private AudienceState _currentState = AudienceState.Idle;
    private Coroutine _stateRoutine;
    private int _stateVersion;
    
    public AudienceState CurrentState => _currentState;

    void Awake()
    {
        animator = GetComponent<Animator>();
        if (animator == null)
            animator = GetComponentInChildren<Animator>();
            
        proceduralAnimator = GetComponent<ProceduralAudienceAnimator>();
        if (proceduralAnimator == null)
            proceduralAnimator = GetComponentInChildren<ProceduralAudienceAnimator>();

        reactionDelay = Random.Range(0f, 3.0f);
        // Kişilik farklılıkları: pozitif = hoşgörülü, negatif = hassas
        personalWpmTolerance = Random.Range(-20f, 20f);
        personalEyeContactTolerance = Random.Range(-0.15f, 0.15f);
    }

    public void SetState(AudienceState newState)
    {
        if (_currentState == newState) return;
        _currentState = newState;
        _stateVersion++;

        if (_stateRoutine != null)
        {
            StopCoroutine(_stateRoutine);
            _stateRoutine = null;
        }

        float delay = newState == AudienceState.Applauding ? 0f : reactionDelay;
        _stateRoutine = StartCoroutine(ApplyStateWithDelay(newState, delay, _stateVersion));
    }

    private IEnumerator ApplyStateWithDelay(AudienceState state, float delay, int version)
    {
        yield return new WaitForSeconds(delay);

        if (version != _stateVersion)
            yield break;
        
        // GÜVENLİK FİX: Eğer ProceduralAnimator Awake anınca bulunamadıysa şimdi tekrar ara
        if (proceduralAnimator == null)
        {
            proceduralAnimator = GetComponent<ProceduralAudienceAnimator>();
            if (proceduralAnimator == null) 
                proceduralAnimator = GetComponentInChildren<ProceduralAudienceAnimator>();
        }
        
        if (proceduralAnimator != null)
        {
            proceduralAnimator.SetState(state);
        }
        else
        {
            Debug.LogError($"[AudienceMember] {gameObject.name} üzerinde ProceduralAudienceAnimator bulunamadı! Sinyal kayboldu.", this);
        }

        _stateRoutine = null;
    }
}
