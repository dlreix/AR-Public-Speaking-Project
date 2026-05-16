using System.Collections;
using UnityEngine;

public class AudienceMember : MonoBehaviour
{
    public Animator animator;
    public ProceduralAudienceAnimator proceduralAnimator;
    public float reactionDelay = 0f;
    public float personalWpmTolerance;
    public float personalEyeContactTolerance;
    [SerializeField] private Vector2 stateDwellSeconds = new Vector2(4f, 10f);
    [SerializeField] private float cooldownSeconds = 5.0f;
    private AudienceState _currentState = AudienceState.Idle;
    private Coroutine _stateRoutine;
    private int _stateVersion;
    private float _nextStateChangeTime;
    private float _cooldownEndTime;
    
    public AudienceState CurrentState => _currentState;
    public bool CanConsiderStateChange => Time.time >= _nextStateChangeTime && Time.time >= _cooldownEndTime;

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
        SetState(newState, false);
    }

    public void SetState(AudienceState newState, bool force)
    {
        if (!force)
        {
            if (_currentState != AudienceState.Idle && Time.time < _nextStateChangeTime)
                return;
            
            if (Time.time < _cooldownEndTime)
            {
                newState = AudienceState.Neutral;
            }
        }

        if (_currentState == newState) return;

        if (_currentState != AudienceState.Idle && _currentState != AudienceState.Neutral && 
            (newState == AudienceState.Idle || newState == AudienceState.Neutral))
        {
            _cooldownEndTime = Time.time + cooldownSeconds;
        }

        if (!force && _currentState != AudienceState.Idle && _currentState != AudienceState.Neutral && 
            newState != AudienceState.Idle && newState != AudienceState.Neutral)
        {
            newState = AudienceState.Neutral;
            _cooldownEndTime = Time.time + cooldownSeconds;
        }

        _currentState = newState;
        _stateVersion++;
        _nextStateChangeTime = Time.time + GetDwellDuration(newState);

        if (_stateRoutine != null)
        {
            StopCoroutine(_stateRoutine);
            _stateRoutine = null;
        }

        float delay = newState == AudienceState.Applauding ? 0f : reactionDelay;
        _stateRoutine = StartCoroutine(ApplyStateWithDelay(newState, delay, _stateVersion));
    }

    private float GetDwellDuration(AudienceState state)
    {
        if (state == AudienceState.Applauding)
        {
            return 0f;
        }

        float min = Mathf.Max(0.5f, stateDwellSeconds.x);
        float max = Mathf.Max(min, stateDwellSeconds.y);
        return Random.Range(min, max);
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
