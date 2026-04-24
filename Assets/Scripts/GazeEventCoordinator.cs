using UnityEngine;

namespace VRPublicSpeaking.MainBranchGaze
{
/// <summary>
/// Gaze event koordinatörü.
///
/// Aynı anda sahnede yalnızca BİR event'in hedef üretmesini garanti eder.
/// Üç event sistemi (CircleEventSystem, QuickGazeDotSystem, MovingGazeDotSystem)
/// bir şey spawn etmeden önce bu koordinatörden kilit ister.
///
/// Kullanım desenleri:
///   • Rastgele event'ler (Quick, Moving) → TryAcquire() kullanır
///     kilit başkasındaysa sessizce pas geçer ve bir dahaki sefere tekrar dener.
///   • Kullanıcı tetiklemeli event (Circle) → ForceAcquire() kullanır
///     kullanıcının butonu öncelik verir; önceki event kapatılır, sonra kilit alınır.
///
/// Sahneye boş bir GameObject'e atanır (örn. "GazeEventCoordinator").
/// </summary>
public class GazeEventCoordinator : MonoBehaviour
{
    // ──────────────────────────────────────────────
    //  DURUM
    // ──────────────────────────────────────────────
    private IGazeEvent currentHolder;

    /// <summary>Şu an aktif bir event var mı?</summary>
    public bool IsHeld => currentHolder != null;

    /// <summary>Kilidi tutan event (bilgi amaçlı).</summary>
    public IGazeEvent CurrentHolder => currentHolder;

    // ══════════════════════════════════════════════
    //  KAMU API
    // ══════════════════════════════════════════════

    /// <summary>
    /// Kilidi almayı dener. Başka bir event varsa false döner.
    /// Rastgele event'ler için kullanılır — başkası çalışıyorsa bir dahaki
    /// sefere tekrar dener, kimseyi rahatsız etmez.
    /// </summary>
    public bool TryAcquire(IGazeEvent requester)
    {
        if (requester == null) return false;

        if (currentHolder != null)
        {
            Debug.Log($"[GazeEventCoordinator] TryAcquire denied: '{currentHolder.GetType().Name}' is running.");
            return false;
        }

        currentHolder = requester;
        Debug.Log($"[GazeEventCoordinator] Lock acquired by '{requester.GetType().Name}'.");
        return true;
    }

    /// <summary>
    /// Kilidi zorla alır. Varsa mevcut event'i ForceStop ile bitirir, sonra kilidi verir.
    /// Kullanıcı tetiklemeli event'ler (Circle) için kullanılır — kullanıcı butonuna bastıysa
    /// anında yanıt vermeliyiz.
    /// </summary>
    public void ForceAcquire(IGazeEvent requester)
    {
        if (requester == null) return;

        if (currentHolder != null && currentHolder != requester)
        {
            Debug.Log($"[GazeEventCoordinator] Preempting '{currentHolder.GetType().Name}' for '{requester.GetType().Name}'.");
            IGazeEvent previous = currentHolder;
            // ForceStop zincirleme Release çağıracak; biz de önce slotu null'a alıyoruz
            // ki Release idempotent kalsın ve biz sonra yeni holder'ı atayabilelim.
            currentHolder = null;
            previous.ForceStop();
        }

        currentHolder = requester;
        Debug.Log($"[GazeEventCoordinator] Lock force-acquired by '{requester.GetType().Name}'.");
    }

    /// <summary>
    /// Kilidi serbest bırakır — sadece çağıran event kilidi tutuyorsa işlem yapar.
    /// Idempotent: yanlış sahip çağırırsa hiçbir şey olmaz.
    /// </summary>
    public void Release(IGazeEvent requester)
    {
        if (requester == null) return;
        if (currentHolder != requester) return;

        currentHolder = null;
        Debug.Log($"[GazeEventCoordinator] Lock released by '{requester.GetType().Name}'.");
    }

    /// <summary>
    /// Varsa mevcut event'i durdur (oturum sonu için).
    /// MainController.StopSession bunu çağırarak tüm event'leri tek seferde kapatabilir.
    /// </summary>
    public void StopAll()
    {
        if (currentHolder == null) return;

        Debug.Log($"[GazeEventCoordinator] StopAll: force-stopping '{currentHolder.GetType().Name}'.");
        IGazeEvent holder = currentHolder;
        currentHolder = null;
        holder.ForceStop();
    }
}
}
