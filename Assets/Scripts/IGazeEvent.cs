/// <summary>
/// Koordinatör tarafından yönetilen tüm gaze event'lerinin uyması gereken sözleşme.
///
/// Bir event ancak bu arayüzü uygulayarak GazeEventCoordinator kilidine katılabilir.
/// ForceStop() çağrıldığında event kendi kaynaklarını temizlemeli ve koordinatör
/// kilidini bırakmalıdır.
/// </summary>
public interface IGazeEvent
{
    /// <summary>Event aktif mi (sahnede bir hedef var mı)?</summary>
    bool IsRunning { get; }

    /// <summary>
    /// Event'i zorla durdur. Koordinatör (önalım/preemption) veya oturum sonu
    /// (MainController.StopSession) tarafından çağrılabilir.
    /// </summary>
    void ForceStop();
}
