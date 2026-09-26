using TMPro;
using UnityEngine;

public static class FontProvider
{
    static TMP_FontAsset font;
    static bool loaded;

    public static void Apply(TMP_Text text)
    {
        if (!loaded)
        {
            loaded = true;
            font = Resources.Load<TMP_FontAsset>("Fonts/PF스타더스트 3.0 SDF");
        }
        if (font != null) text.font = font;
    }
}
