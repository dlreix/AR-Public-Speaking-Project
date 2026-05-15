using UnityEngine;
using TMPro;
using System.IO;
using VRPublicSpeaking.AppShell.UI;

public class LoginController : MonoBehaviour
{
    [Header("UI Referanslarý")]
    public TMP_InputField usernameInput;
    public TextMeshProUGUI warningText;

    [Header("Panel Referanslarý (AppPanelView Kullanarak)")]
    public AppPanelView loginPanel;
    public AppPanelView homePanel;

    void Start()
    {
        if (warningText != null)
            warningText.text = "";
    }

    public void OnLoginButtonClicked()
    {
        string enteredName = usernameInput.text.Trim();

        // 1. KONTROL: Ýsim boþ mu?
        if (string.IsNullOrEmpty(enteredName))
        {
            warningText.text = "Lütfen bir kullanýcý adý girin!";
            return;
        }

        // --- DEÐÝÞEN KISIM BURASI ---
        string expectedFilePath = Application.persistentDataPath + $"/history_{enteredName}.json";

        // Eðer dosya varsa "Kayýtlý Kullanýcý Giriþ Yaptý" diyeceðiz.
        // Eðer dosya yoksa "Yeni Kullanýcý Oluþturuldu" diyeceðiz.
        if (File.Exists(expectedFilePath))
        {
            Debug.Log($"[LoginController] Kayýtlý profil bulundu: {enteredName}. Bilgiler yükleniyor...");
        }
        else
        {
            Debug.Log($"[LoginController] Yeni profil oluþturuluyor: {enteredName}.");
        }
        // ------------------------------

        warningText.text = "";

        // Kullanýcýyý DataManager'a bildir (Ýsim varsa yükler, yoksa sýfýrdan oluþturur)
        if (DataManager.Instance != null)
        {
            DataManager.Instance.SetUser(enteredName);
        }
        else
        {
            Debug.LogError("[LoginController] DataManager sahnede bulunamadý!");
        }

        // Panelleri deðiþtir
        if (loginPanel != null) loginPanel.Hide();
        if (homePanel != null) homePanel.Show();
    }
}