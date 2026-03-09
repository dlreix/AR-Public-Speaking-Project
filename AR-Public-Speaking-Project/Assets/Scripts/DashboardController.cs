using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class DashboardController : MonoBehaviour
{
    [Header("Performance Stats")]
    public Image eyeContactBar;
    public TextMeshProUGUI eyeContactText;

    public Image speakingPaceBar;
    public TextMeshProUGUI speakingPaceText;

    [Header("Overview Stats")]
    public TextMeshProUGUI mainScoreText;

    // Bu fonksiyonu butona baðlayacaðýz
    public void SimulateData()
    {
        // Rastgele deðerler oluþtur (0 ile 1 arasý bar için, 0-100 arasý yazý için)
        float eyeVal = Random.Range(0.6f, 0.95f);
        float paceVal = Random.Range(0.4f, 0.8f);
        int mainScore = Random.Range(70, 95);

        // Görselleþtirmeyi güncelle
        if (eyeContactBar) eyeContactBar.fillAmount = eyeVal;
        if (eyeContactText) eyeContactText.text = "%" + (eyeVal * 100).ToString("F0");

        if (speakingPaceBar) speakingPaceBar.fillAmount = paceVal;
        if (speakingPaceText) speakingPaceText.text = (paceVal * 200).ToString("F0") + " WPM";

        if (mainScoreText) mainScoreText.text = mainScore.ToString();

        Debug.Log("Veriler Simüle Edildi!");
    }
}