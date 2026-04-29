using UnityEngine;
using Random = System.Random;

public class AudienceVisualizer : MonoBehaviour
{
    [Header("Color Palettes")]
    public Color[] skinTones = new Color[] {
        new Color(0.98f, 0.85f, 0.73f), // Light
        new Color(0.94f, 0.76f, 0.60f), // Medium Light
        new Color(0.82f, 0.58f, 0.40f), // Medium
        new Color(0.55f, 0.35f, 0.20f), // Dark
        new Color(0.35f, 0.20f, 0.10f), // Very Dark
        new Color(0.72f, 0.48f, 0.30f)
    };

    public Color[] shirtColors = new Color[] {
        new Color(0.86f, 0.87f, 0.89f),
        new Color(0.72f, 0.78f, 0.86f),
        new Color(0.43f, 0.49f, 0.58f),
        new Color(0.22f, 0.27f, 0.34f),
        new Color(0.78f, 0.74f, 0.68f),
        new Color(0.55f, 0.43f, 0.35f),
        new Color(0.48f, 0.18f, 0.16f),
        new Color(0.18f, 0.29f, 0.24f)
    };

    public Color[] pantsColors = new Color[] {
        new Color(0.12f, 0.15f, 0.23f),
        new Color(0.18f, 0.19f, 0.22f),
        new Color(0.30f, 0.29f, 0.27f),
        new Color(0.38f, 0.33f, 0.26f),
        new Color(0.22f, 0.27f, 0.21f)
    };

    public Color[] hairColors = new Color[] {
        new Color(0.08f, 0.07f, 0.06f),
        new Color(0.20f, 0.13f, 0.09f),
        new Color(0.37f, 0.25f, 0.16f),
        new Color(0.58f, 0.48f, 0.20f),
        new Color(0.70f, 0.70f, 0.68f)
    };

    void Start()
    {
        RandomizeAppearance();
    }

    public void RandomizeAppearance()
    {
        Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
        if (renderers == null || renderers.Length == 0) return;

        Random seededRandom = new Random(GetStableSeed());
        Color skin = skinTones[seededRandom.Next(0, skinTones.Length)];
        Color shirt = shirtColors[seededRandom.Next(0, shirtColors.Length)];
        Color pants = pantsColors[seededRandom.Next(0, pantsColors.Length)];
        Color hair = hairColors[seededRandom.Next(0, hairColors.Length)];

        foreach (Renderer rend in renderers)
        {
            Material[] mats = rend.materials;
            for (int i = 0; i < mats.Length; i++)
            {
                Material materialInstance = new Material(mats[i]);
                string key = (rend.name + "_" + mats[i].name).ToLower();
                Color targetColor = ResolveTargetColor(key, i, mats.Length, skin, shirt, pants, hair, materialInstance);
                ApplyColor(materialInstance, targetColor, mats.Length);
                mats[i] = materialInstance;
            }
            rend.materials = mats;
        }
    }

    private int GetStableSeed()
    {
        int hash = gameObject.name.GetHashCode();
        hash ^= Mathf.RoundToInt(transform.position.x * 100f);
        hash ^= Mathf.RoundToInt(transform.position.z * 100f) << 2;
        return hash;
    }

    private Color ResolveTargetColor(string key, int materialIndex, int materialCount, Color skin, Color shirt, Color pants, Color hair, Material original)
    {
        if (key.Contains("hair") || key.Contains("brow") || key.Contains("lash"))
            return hair;

        if (key.Contains("skin") || key.Contains("face") || key.Contains("head") || key.Contains("ear") || key.Contains("hand"))
            return skin;

        if (key.Contains("pant") || key.Contains("leg") || key.Contains("bottom") || key.Contains("shoe"))
            return pants;

        if (key.Contains("shirt") || key.Contains("top") || key.Contains("body") || key.Contains("cloth") || key.Contains("torso") || key.Contains("jacket"))
            return shirt;

        if (materialCount == 1)
            return BlendWithOriginal(original, shirt, 0.35f);

        if (materialCount == 2)
            return materialIndex == 0 ? BlendWithOriginal(original, skin, 0.55f) : BlendWithOriginal(original, shirt, 0.45f);

        if (materialCount >= 3)
        {
            if (materialIndex == 0) return BlendWithOriginal(original, skin, 0.6f);
            if (materialIndex == 1) return BlendWithOriginal(original, shirt, 0.5f);
            if (materialIndex == 2) return BlendWithOriginal(original, pants, 0.5f);
        }

        return BlendWithOriginal(original, shirt, 0.35f);
    }

    private Color BlendWithOriginal(Material original, Color target, float strength)
    {
        Color baseColor = original.HasProperty("_BaseColor") ? original.GetColor("_BaseColor") :
            original.HasProperty("_Color") ? original.color : Color.white;
        return Color.Lerp(baseColor, target, strength);
    }

    private void ApplyColor(Material mat, Color color, int materialCount)
    {
        if (mat.HasProperty("_BaseColor"))
            mat.SetColor("_BaseColor", color);
        if (mat.HasProperty("_Color"))
            mat.color = color;

        if (mat.HasProperty("_EmissionColor"))
        {
            Color emission = color * 0.06f;
            mat.SetColor("_EmissionColor", emission);
        }

        if (mat.HasProperty("_Smoothness"))
            mat.SetFloat("_Smoothness", materialCount == 1 ? 0.28f : 0.18f);
    }
}
