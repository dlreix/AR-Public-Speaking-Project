using System;
using System.Collections.Generic;
using UnityEngine;

namespace PresentationAnalyzer
{
    /// <summary>
    /// Arka plan thread'lerinden Unity ana thread'ine güvenli eylem aktarımı.
    /// Unity'de UI güncellemeleri, Debug.Log ve çoğu API yalnızca
    /// ana thread'den çağrılabilir. Bu sınıf o köprüyü kurar.
    ///
    /// Kullanım:
    ///   MainThreadDispatcher.Enqueue(() => myText.text = "Merhaba");
    /// </summary>
    public class MainThreadDispatcher : MonoBehaviour
    {
        // ─── Singleton ────────────────────────────────────────────────────────

        private static MainThreadDispatcher _instance;

        /// <summary>
        /// Sahnede yoksa otomatik oluşturur — manuel yerleştirmeye gerek yok.
        /// </summary>
        public static MainThreadDispatcher Instance
        {
            get
            {
                if (_instance == null)
                {
                    GameObject go = new GameObject("MainThreadDispatcher");
                    _instance = go.AddComponent<MainThreadDispatcher>();

                    // Sahne geçişlerinde yok edilmesin
                    DontDestroyOnLoad(go);
                }
                return _instance;
            }
        }

        // ─── Eylem kuyruğu ────────────────────────────────────────────────────

        // İki kuyruk tekniği: arka plan thread yazarken ana thread
        // diğer kuyruğu işler — lock süresi minimumda kalır.
        private readonly Queue<Action> _queue        = new Queue<Action>();
        private readonly Queue<Action> _executionQueue = new Queue<Action>();
        private readonly object        _lock         = new object();

        // ─── Public API ───────────────────────────────────────────────────────

        /// <summary>
        /// Herhangi bir thread'den çağrılabilir.
        /// Verilen Action bir sonraki Update'te ana thread'de çalışır.
        /// </summary>
        public static void Enqueue(Action action)
        {
            if (action == null) return;

            lock (Instance._lock)
            {
                Instance._queue.Enqueue(action);
            }
        }

        // ─── Unity yaşam döngüsü ──────────────────────────────────────────────

        private void Awake()
        {
            // Sahnede zaten bir tane varsa kendini yok et
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
            DontDestroyOnLoad(gameObject);
        }

        /// <summary>
        /// Her frame ana thread'de çalışır.
        /// Kuyruktaki tüm eylemleri tek tek çalıştırır.
        /// </summary>
        private void Update()
        {
            // Kuyruğu kilitle ve çalıştırma kuyruğuna taşı
            lock (_lock)
            {
                while (_queue.Count > 0)
                    _executionQueue.Enqueue(_queue.Dequeue());
            }

            // Kilitsiz çalıştır — bu kısım yalnızca ana thread'e aittir
            while (_executionQueue.Count > 0)
            {
                Action action = _executionQueue.Dequeue();

                try
                {
                    action.Invoke();
                }
                catch (Exception e)
                {
                    // Bir eylem hata verse bile diğerleri çalışmaya devam eder
                    Debug.LogError($"[MainThreadDispatcher] Eylem hatası: {e.Message}");
                }
            }
        }
    }
}