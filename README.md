# Module Integration Guide for the App Shell Menu

This document explains how other project members can connect their own modules to the UI shell, main menu, session setup, pause menu, and results flow without breaking the existing demo flow.

The goal is simple: each module should plug into the shell through the existing adapter/state system instead of directly editing unrelated menu logic.

## 1. Current Shell Structure

The menu system is generated and controlled mainly from these files:

- `Assets/AppShell/Editor/AppShellSceneGenerator.cs`
- `Assets/AppShell/Runtime/Core/AppRuntimeState.cs`
- `Assets/AppShell/Runtime/Flow/AppFlowManager.cs`
- `Assets/AppShell/Runtime/UI/SessionConfigController.cs`
- `Assets/AppShell/Runtime/UI/EnvironmentSessionOverlayController.cs`
- `Assets/AppShell/Runtime/Results/ResultsFlowController.cs`
- `Assets/AppShell/Runtime/Results/DashboardAdapter.cs`
- `Assets/AppShell/Runtime/Integration/EnvironmentSceneInstaller.cs`

Important rule:

- Do not manually rebuild the whole menu in the scene unless necessary.
- Prefer connecting your module through an adapter or presenter field.
- If a screen needs to be generated consistently, update the generator instead of only editing one scene object by hand.

## 2. Integration Points by Module Type

### Dashboard Module

The shell already has Dashboard Entry buttons in:

- Review Center / Progress panel
- Results Summary panel

These buttons call `DashboardAdapter`.

Relevant file:

- `Assets/AppShell/Runtime/Results/DashboardAdapter.cs`

How to connect:

1. Add the dashboard UI root object or dashboard controller to the scene.
2. Select the object that has `DashboardAdapter`.
3. Assign one of these fields:
   - `dashboardRoot`: use this if opening the dashboard only requires activating a GameObject.
   - `dashboardController`: use this if your dashboard has an open method.
4. If using a controller, the adapter can call one of these methods automatically:
   - `Open`
   - `OpenDashboard`
   - `ShowDashboard`
   - `Show`
5. Test from the Results Summary screen by pressing `Dashboard Entry`.

Expected behavior:

- If wired correctly, Dashboard Entry opens the real dashboard.
- If not wired, the shell shows a placeholder message and logs that no dashboard integration is connected.

Recommended controller example:

```csharp
public class DashboardController : MonoBehaviour
{
    public void OpenDashboard()
    {
        gameObject.SetActive(true);
        // Refresh dashboard data here.
    }
}
```

## 3. Adding Data to the Results Screen

The results screen reads data from `SessionResultSummary`.

Relevant files:

- `Assets/AppShell/Runtime/Data/SessionResultSummary.cs`
- `Assets/AppShell/Runtime/Results/ResultsSummaryPresenter.cs`
- `Assets/AppShell/Runtime/Integration/ScoringAdapter.cs`
- `Assets/AppShell/Runtime/Core/AppRuntimeState.cs`

Current supported result fields:

- `TotalScore`
- `EyeContactScore`
- `SpeechPaceScore`
- `PostureScore`
- `FillerWordCount`
- `DurationSeconds`
- `StrongestArea`
- `WeakestArea`
- `PerformanceBand`
- `Recommendations`

How result data reaches the UI:

1. A session ends.
2. `ExistingSceneFlowAdapter` calls `ScoringAdapter.CaptureSummary(...)`.
3. `ScoringAdapter` creates a `SessionResultSummary`.
4. `AppRuntimeState.StoreResult(summary)` stores it.
5. `ResultsSummaryPresenter.Refresh()` displays it.

If your module produces result data:

- Add the data into `ScoringAdapter.CaptureSummary(...)` if it belongs in the existing summary.
- If a new result field is required, add it to `SessionResultSummary`.
- Then update `ResultsSummaryPresenter` only if the shell summary needs to display it.

Important:

- Do not make the results screen directly search for every module.
- Let the module pass data into `SessionResultSummary` through an adapter.

## 4. Voice Analysis Integration

The Session Setup screen already contains a `Voice Analysis` option.

Relevant files:

- `Assets/AppShell/Runtime/Data/SessionConfig.cs`
- `Assets/AppShell/Runtime/UI/SessionConfigController.cs`
- `Assets/AppShell/Runtime/Integration/ScoringAdapter.cs`

Current config flag:

```csharp
config.VoiceAnalysisEnabled
```

Recommended integration steps:

1. In the voice analysis module, read the current config from:

```csharp
AppRuntimeState.GetOrCreate().CurrentSessionConfig
```

2. Only run voice analysis if:

```csharp
CurrentSessionConfig.VoiceAnalysisEnabled
```

3. During or after the session, send speech metrics into the scoring/result layer.
4. If using the existing results screen, map values into:
   - `SpeechPaceScore`
   - `FillerWordCount`
   - `Recommendations`
5. If the module has a separate dashboard view, connect it through `DashboardAdapter`.

Minimum expected behavior:

- If Voice Analysis is enabled in setup, the voice module should start during the live session.
- If it is disabled, it should not collect or affect score data.

## 5. Posture Analysis Integration

The Session Setup screen already contains a `Posture Analysis` option.

Current config flag:

```csharp
config.PostureAnalysisEnabled
```

Recommended integration steps:

1. Read the flag from `AppRuntimeState.CurrentSessionConfig`.
2. Enable/disable posture processing based on `PostureAnalysisEnabled`.
3. Push final posture score into:

```csharp
SessionResultSummary.PostureScore
SessionResultSummary.HasPostureScore
```

4. If extra feedback is generated, add it to `Recommendations`.

Important:

- If posture is not fully implemented, keep the toggle disabled or clearly mark it as incomplete for the demo.

## 6. Eye Tracking / Gaze Scoring Integration

The shell already connects gaze-related systems through:

- `EnvironmentSceneInstaller`
- `TrackingAdapter`
- `ScoringAdapter`

Relevant files:

- `Assets/AppShell/Runtime/Integration/EnvironmentSceneInstaller.cs`
- `Assets/AppShell/Runtime/Integration/TrackingAdapter.cs`
- `Assets/AppShell/Runtime/Integration/ScoringAdapter.cs`

Current config flags:

```csharp
config.EyeTrackingEnabled
config.GazeScoringEnabled
```

Current behavior:

- `EnvironmentSceneInstaller` tries to find or create the runtime tracking stack.
- `TrackingAdapter` enables/disables tracking based on session config.
- `ScoringAdapter` captures gaze score at session end.

If you update gaze or eye tracking:

- Keep `EyeTrackingSystem` discoverable in the scene.
- Keep `GazeScoringSystem.eyeTracking` assigned.
- Do not create duplicate competing gaze systems.
- Make sure pause state is respected so paused sessions do not collect false samples.

Pause-safe expectation:

- During pause, tracking/scoring should stop or ignore samples.
- After resume, tracking/scoring should continue normally.

## 7. Adding a New Menu Button

If a new button is needed in the main menu or another shell panel:

1. Decide which panel owns it:
   - Main Hub
   - Settings
   - Review Center
   - Results Summary
   - Session Setup
2. Add the button in `AppShellSceneGenerator`.
3. Add a public method in the related presenter/controller.
4. Wire the button using `AppShellEditorCommon.SetButtonEvent(...)`.
5. Keep the action safe if the target module is not connected yet.

Example pattern:

```csharp
public void OpenMyModule()
{
    if (myModuleAdapter != null && myModuleAdapter.TryOpen())
    {
        SetNote("Module opened.");
        return;
    }

    SetNote("Module is not connected yet.");
}
```

Important:

- Do not make buttons silently fail.
- If a module is missing, show a short status message.
- If the feature is not ready for demo, mark it as staged or coming soon.

## 8. Adding a New Setup Option

If a module needs a setup toggle or setting:

1. Add the field to `SessionConfig`.
2. Add UI control in `AppShellSceneGenerator.BuildSessionSetupPanel(...)`.
3. Add serialized field in `SessionConfigController`.
4. Update:
   - `BuildConfigSnapshot()`
   - `LoadFromRuntime()`
   - `RefreshSummaryPreview()`
5. Read the value from `AppRuntimeState.CurrentSessionConfig` in your module.

Do not store module settings only inside UI objects.

Correct data direction:

```text
Session Setup UI -> SessionConfig -> AppRuntimeState -> Environment Scene / Module
```

## 9. Connecting a Module Inside Environment Scenes

Environment scenes are prepared through `EnvironmentSceneInstaller`.

Current environment scenes:

- `Scene_Classroom`
- `Scene_ConferenceHall`
- `Scene_MeetingRoom`

If your module must exist in every environment:

1. Prefer adding or resolving it inside `EnvironmentSceneInstaller`.
2. Make sure it can be found with `FindFirstObjectByType<T>(FindObjectsInactive.Include)` if needed.
3. Avoid scene-specific hardcoding unless absolutely required.
4. Test in all three environments.

Good rule:

- If the module is required for every session, installer/adapter level is the right place.
- If the module is only visual or scene-specific, scene object wiring is acceptable.

## 10. Pause Menu Compatibility

During a live session, the shell uses `EnvironmentSessionOverlayController`.

Relevant file:

- `Assets/AppShell/Runtime/UI/EnvironmentSessionOverlayController.cs`

Pause behavior:

- Resume
- Restart Session
- End Session
- Return To Hub

Module requirement:

- Any module that records runtime samples must be pause-safe.
- Do not keep collecting analysis data while the session is paused.

Recommended module methods:

```csharp
public void PauseModule()
{
    // Stop collecting runtime samples.
}

public void ResumeModule()
{
    // Continue collecting samples.
}
```

If your module already depends on `MainController` or tracking systems, verify that pause does not corrupt your data.

## 11. Scene Routing Rules

Use existing flow methods instead of directly loading scenes from random module scripts.

Common routes:

- Main hub: `AppPanelType.Home`
- Environment selection: `AppPanelType.EnvironmentSelection`
- Session setup: `AppPanelType.SessionSetup`
- Results summary: `AppPanelType.ResultsSummary`

If a module needs to request a hub panel:

```csharp
AppRuntimeState.GetOrCreate().RequestHubPanel(AppPanelType.ResultsSummary);
```

Then route back to the hub scene through the existing flow/transition system.

Avoid:

```csharp
SceneManager.LoadScene("SomeScene");
```

unless the module is specifically responsible for scene loading.

## 12. Demo Readiness Checklist for Module Owners

Before saying a module is integrated, check:

- The module works from the shell flow, not only from a standalone test scene.
- The module respects Session Setup config.
- The module works in Classroom, Conference Hall, and Meeting Room if required.
- The module does not break pause/resume.
- The module does not create duplicate cameras, event systems, or audio listeners.
- The module does not require macOS-only paths.
- The module has a safe fallback if it is not connected.
- The Unity Console has no new errors after a full run.

Recommended full flow:

```text
Main Hub
-> Practice Mode
-> Environment Selection
-> Session Setup
-> Live Session
-> Pause / Resume
-> End Session
-> Results Overlay
-> Dashboard or Return To Hub
```

## 13. What Not To Do

Avoid these patterns:

- Do not directly edit generated UI objects without updating the generator if the change must persist.
- Do not create a separate main menu for your module.
- Do not duplicate `AppRuntimeState`.
- Do not bypass `SessionConfig` for settings that belong to session setup.
- Do not collect scoring data while paused.
- Do not assume only one environment scene exists.
- Do not make a button fail silently when your module is missing.

## 14. Quick Integration Summary

Use this mapping:

| Module Need | Best Integration Point |
| --- | --- |
| Add final dashboard | `DashboardAdapter` |
| Add result metrics | `ScoringAdapter` + `SessionResultSummary` |
| Read setup options | `AppRuntimeState.CurrentSessionConfig` |
| Add setup toggle | `SessionConfig` + `SessionConfigController` + generator |
| Add environment runtime dependency | `EnvironmentSceneInstaller` |
| Add menu navigation | `AppFlowManager` / panel presenter |
| Support pause | `MainController` events or module-level pause methods |
| Show post-session data | `ResultsSummaryPresenter` or dashboard module |

The safest approach is to connect modules through small adapters and keep the shell responsible only for navigation, setup state, pause/results display, and demo flow.

---

# Proje Mimarisi ve Entegrasyon Raporu (Turkish)

Bu bölüm, projede yer alan modüllerin, sahnelerin ve kod yapılarının nasıl çalıştığını takım üyelerine detaylıca açıklamak amacıyla eklenmiştir. Proje, "Modüler ve Gevşek Bağlı (Loosely Coupled)" bir mimariyle inşa edilmiş olup, her sistemin kendi işini bağımsız yaptığı ve birbirlerine "Adapter" (Adaptör) scriptleri üzerinden bağlandığı modern bir yapıya sahiptir.

## 1. Genel Mimari Özeti

Proje temel olarak **4 ana yapıtaşından** oluşmaktadır:

1. **App Shell (Ana Çatı ve UI):** Kullanıcının ana menüde (Dashboard) ayarları yaptığı, eğitim sahnelerini başlatan, oturum (session) durumunu yöneten ve HUD (gözlük içi uyarı) ekranlarını çizen temel sistem.
2. **Audience Simulation (Seyirci Simülasyonu):** Salonda oturan seyircilerin davranışlarını, tepkilerini ve animasyonlarını otonom olarak yöneten sistem (Arda'nın modülü).
3. **Speech Pipeline (Ses ve Konuşma Analizi):** Çevrimdışı (offline) Vosk kütüphanesini kullanarak sesi metne çeviren, konuşma hızını (WPM) ve dolgu kelimelerini hesaplayan motor.
4. **Performance Scoring (Puanlama Motoru):** Kullanıcının göz teması, duruş (posture) ve ses analizi sonuçlarını birleştirip oturum sonunda 100 üzerinden nihai bir skor ve geri bildirim raporu üreten sistem.

## 2. Temel Modüller ve Çalışma Mantıkları

### A. App Shell ve Otomatik Kurulum (EnvironmentSceneInstaller)
Sistemde, içi script dolu karışık ve ağır sahneler (prefab'lar) tutmak yerine **"Dinamik Kurulum (Runtime Injection)"** mantığı kullanılır.
- **`EnvironmentSceneInstaller.cs`**: Bir ortam sahnesi (Örn: Classroom veya Conference Hall) yüklendiğinde otomatik olarak çalışır. Sahnede eksik olan tüm sistemleri (Puanlama motoru, Seyirci yöneticisi, Ses adaptörü) kontrol eder, eğer yoklarsa **kod ile anında yaratıp sahneye ekler**. Bu sayede sahneler temiz kalır, her yeni sahnede "Acaba şu scripti eklemeyi unuttum mu?" derdi ortadan kalkar.
- **`AppRuntimeState.cs` & `SessionConfig.cs`**: Seçilen zorluk seviyesi, süre limitleri ve hangi analizlerin açık olacağı (Eye Tracking, Voice Analysis vb.) merkezi olarak burada tutulur. Tüm modüller ayarları buradan okur.

### B. Audience Simulation (Seyirci Sistemi)
Seyirci modülü ortamdan bağımsız, otonom bir şekilde çalışır.
- **`AudienceSpawner.cs`**: Sahne açıldığında sahnedeki "Bench" (Bank) veya "Seat" (Koltuk) isimli objeleri bulur. Boyutlarına göre bu bankları sanal koltuklara böler ve üzerlerine rastgele 3D seyirci karakterleri (Remy, Ch07 vb.) oturtur. Karakterlerin boyunu ve masaya olan yüksekliklerini geometriye göre otomatik ayarlar.
- **`AudienceBehaviorController.cs` & `AudienceReactionEngine.cs`**: Kullanıcının konuşma hızı (WPM) çok düşerse veya göz temasından kaçınırsa, seyircilerin "Stres/Sıkılma" (Bored) seviyesi artar. Animasyonlar (dikkatli dinleme, alkışlama, esneme) mevcut skora göre prosedürel (dinamik) olarak değişir.

### C. Speech Pipeline (Ses ve Konuşma Analizi)
- **`SpeechPipelineController.cs` & `VoskSTTEngine.cs`**: Kullanıcının mikrofonundan gelen sesi alır. 1.8 GB'lık ağır modeller yerine Unity için optimize edilmiş **40 MB'lık (vosk-model-small)** çevrimdışı dil modelini kullanarak sesi anlık metne döker.
- **Gizlilik ve Performans:** Bu işlem tamamen cihazın RAM'inde gizli olarak yapılır, hiçbir ses dosyası (.wav vb.) diske kaydedilmez. İnternet bağlantısı gerektirmez.
- Konuşulan kelime sayısı, sessiz kalınan süre ve "ııı, eee" gibi dolgu kelimelerinin sayısını hesaplayarak **`SpeechAdapter.cs`** üzerinden doğrudan Puanlama Motoruna iletir.

### D. Gaze & Event Systems (Göz Takibi ve Etkileşim)
- **`MainController.cs`**: Kullanıcının VR başlığının nereye baktığını (Gaze) takip eder. "Kafanı çok hızlı çeviriyorsun" veya "Çok uzun süredir aynı yere bakıyorsun" şeklindeki anlık uyarıları HUD üzerine yansıtır. Eski veya çakışan Canvas arayüzleri varsa, bunları başlangıçta **otomatik olarak bularak yokedip (Auto-Cleanup)** ekranın bozulmasını engeller.
- **`GazeEventCoordinator.cs`**: Sahnede çıkacak odaklanma noktalarını (Circle Event vb.) yönetir, aynı anda birden fazla hedefin belirmesini engeller.

### E. Performance Scoring (Puanlama ve Geri Bildirim)
- **`PerformanceScoringEngine.cs`**: Oturum (Session) bittiği anda devreye girer. Ses motorundan WPM'i, Göz takibinden "Göz Teması Yüzdesini", Duruş motorundan "Kambur Durma" verilerini çeker.
- Matematiksel bir ağırlıklandırma (Örn: %40 Konuşma, %35 Göz Teması, %25 Duruş) yaparak 100 üzerinden nihai bir skor çıkarır.
- En güçlü (Strongest) ve en zayıf (Weakest) yönleri belirleyip kullanıcıya metin bazlı koçluk geri bildirimi (Örn: "Çok fazla duraksadınız, hızınızı artırın") sunar.

## 3. Sahnelerin (Environments) Yapısı
Projede her bir masanın veya sıranın elle yerleştirildiği manuel sahneler yerine **Kod ile üretilen (Procedural)** sahneler kullanılmıştır:
- **`ClassroomGenerator.cs` & `ConferenceHallGenerator.cs`**: Editor içinde tek bir butona basılarak sınıf veya konferans salonunun geometrisi, aydınlatmaları ve oturma düzeni sıfırdan yaratılır. Bu sayede salonun boyutu, koridor genişliği veya kavisli yapısı tek bir parametre değiştirilerek saniyeler içinde baştan çizilebilir.

## 4. Takım İçin Geliştirme Notları ve Kurallar

1. **Vosk Dil Modeli Hakkında:** 
   Ses analiz motoru artık `Assets/StreamingAssets/vosk-model-small-en-us-0.15` (veya TR modeli) üzerinden çalışmaktadır. Repoyu GitHub'dan yeni çeken takım üyelerinin modeli internetten indirip zipten çıkararak bu tam isimle `StreamingAssets` içine atması şarttır. (Büyük dosyalar GitHub .gitignore sınırlarına takıldığı için repo'da bulunmaz).

2. **Dinamik Bağımlılıklar (Dependencies):** 
   Test yaparken sahnelerdeki objeleri (Puanlama Motoru, Göz Takip objesi vb.) yanlışlıkla silseniz bile sistem çökmez; `EnvironmentSceneInstaller` başlatıldığı anda gereken tüm altyapıyı kodla yeniden inşa eder. Bu özellik size UI veya mekanik testlerinde büyük özgürlük sağlar.

3. **Eski UI ve Arayüz Çakışmaları:** 
   Projenin önceki safhalarında kalan eski "Test Canvas"ları `MainController` tarafından sahnede tespit edildiğinde otomatik olarak silinmektedir. Eğer yeni arayüzler veya paneller tasarlayacaksanız, bunları eski sisteme değil doğrudan `AppShell` sisteminin "Overlay" yapısına veya yeni Prefab'lara entegre etmeniz gerekmektedir.

*Bu doküman, sistem mimarisinin güncel ve kararlı sürümünü referans almaktadır.*
