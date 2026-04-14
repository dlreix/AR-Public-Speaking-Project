using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// FINAL CLEAN VERSION: No instantiation, no shells.
/// Directly modifies material properties while preserving original textures (tinting).
/// Follows the "Index 0 = Skin" rule for maximum reliability.
/// </summary>
public class AudienceVisualEnhancer : MonoBehaviour
{
    // --- REALISTIC SKIN TONES ---
    private static readonly Color SkinLight = new Color(255f/255f, 220f/255f, 185f/255f);
    private static readonly Color SkinMedium = new Color(198f/255f, 134f/255f, 66f/255f);
    private static readonly Color SkinDark = new Color(92f/255f, 51f/255f, 23f/255f);

    private static readonly Color HairBlack = new Color(0.05f, 0.05f, 0.05f);
    private static readonly Color HairBrown = new Color(0.3f, 0.15f, 0.05f);
    private static readonly Color HairBlonde = new Color(0.81f, 0.73f, 0.44f);
    private static readonly Color HairRed = new Color(0.6f, 0.2f, 0.1f);

    // --- DIVERSE CLASSROOM PALETTE ---
    private static readonly Color[] StudentColors = {
        new Color(0.12f, 0.28f, 0.55f), // Royal Blue
        new Color(0.58f, 0.12f, 0.12f), // Crimson
        new Color(0.15f, 0.38f, 0.23f), // Forest Green
        new Color(0.20f, 0.20f, 0.23f), // Charcoal
        new Color(0.72f, 0.54f, 0.05f), // Harvest Gold
        new Color(0.38f, 0.20f, 0.48f), // Deep Purple
        new Color(0.05f, 0.05f, 0.18f), // Navy
        new Color(0.48f, 0.24f, 0.12f), // Terra Cotta
        new Color(0.45f, 0.45f, 0.48f), // Slate Gray
    };

    void Start()
    {
        // 1. Cleanup old system attempts (Important for IK/Animation stability)
        var oldVis = GetComponent<AudienceVisualizer>();
        if (oldVis != null) Destroy(oldVis);
        
        foreach (Transform child in transform) {
            if (child.name.Contains("_Shell")) {
                Destroy(child.gameObject);
                Debug.Log("[VisualEnhancer] Cleaned up legacy shell: " + child.name);
            }
        }

        ApplyCleanVisuals();
    }

    public void ApplyCleanVisuals()
    {
        SkinnedMeshRenderer[] smrs = GetComponentsInChildren<SkinnedMeshRenderer>(true);
        
        // Pick characteristics for this specific student
        Color charSkin = GetRandomSkin();
        Color charHair = GetRandomHair();
        Color charCloth = StudentColors[Random.Range(0, StudentColors.Length)];

        foreach (var smr in smrs)
        {
            string n = smr.name.ToLower();
            Material[] mats = smr.materials; // Creates instances

            for (int i = 0; i < mats.Length; i++)
            {
                string matName = mats[i].name.ToLower();
                string fullPath = (n + "_" + matName).ToLower();

                // --- SURGICAL LOGIC ---

                // A) HAIR DETECTION
                if (fullPath.Contains("hair") || fullPath.Contains("brow") || fullPath.Contains("lash"))
                {
                    TintMaterial(mats[i], charHair, 0.1f);
                }
                // B) FACE/SKIN PROTECTION (Specific objects)
                else if (fullPath.Contains("face") || fullPath.Contains("head") || 
                         fullPath.Contains("eye") || fullPath.Contains("mouth") || fullPath.Contains("teeth"))
                {
                    TintMaterial(mats[i], charSkin, 0.3f);
                }
                // C) THE BODY MESH (Index-Based Split)
                else if (n.Contains("body") || n.Contains("surface") || n.Contains("alpha"))
                {
                    // Index 0 in Mixamo models is almost always Skin.
                    // Index 1+ is Clothing.
                    if (i == 0) TintMaterial(mats[i], charSkin, 0.3f);
                    else TintMaterial(mats[i], charCloth, 0.2f);
                }
                // D) EXPLICIT CLOTHING MESHES
                else if (fullPath.Contains("cloth") || fullPath.Contains("shirt") || fullPath.Contains("pant") || 
                         fullPath.Contains("jacket") || fullPath.Contains("suit") || fullPath.Contains("shoe"))
                {
                    TintMaterial(mats[i], charCloth, 0.2f);
                }
                // E) FALLBACK
                else
                {
                    // Better to tint as clothes if we don't know
                    TintMaterial(mats[i], charCloth, 0.2f);
                }
            }

            smr.materials = mats; // Re-assign array to apply instances
        }
    }

    private void TintMaterial(Material m, Color c, float smoothness)
    {
        // TINTING vs REPLACING:
        // We do NOT clear textures. Instead, we tint the existing texture 
        // using _BaseColor / _Color. This keeps eyes, eyebrows, and cloth folds visible!
        
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
        if (m.HasProperty("_Color")) m.color = c;
        
        m.SetFloat("_Smoothness", smoothness);
        
        // Ensure no weird emission bugs
        if (m.HasProperty("_EmissionColor")) m.SetColor("_EmissionColor", Color.black);
    }

    private Color GetRandomSkin() {
        float r = Random.value;
        if (r < 0.33f) return SkinLight;
        if (r < 0.66f) return SkinMedium;
        return SkinDark;
    }

    private Color GetRandomHair() {
        float r = Random.value;
        if (r < 0.40f) return HairBlack;
        if (r < 0.70f) return HairBrown;
        if (r < 0.90f) return HairBlonde;
        return HairRed;
    }
}
