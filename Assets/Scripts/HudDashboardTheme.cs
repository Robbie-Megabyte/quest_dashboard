using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Shared visual palette matching the web Teleop Control Center.
/// This class contains visual colors only and does not alter interaction
/// geometry, colliders, snap targets, or input behavior.
/// </summary>
public static class HudDashboardTheme
{
    public static readonly Color Background =
        new Color32(11, 14, 12, 255);

    public static readonly Color Panel =
        new Color32(17, 21, 18, 255);

    public static readonly Color PanelTranslucent =
        new Color32(17, 21, 18, 245);

    public static readonly Color Control =
        new Color32(22, 27, 23, 255);

    public static readonly Color ControlHover =
        new Color32(36, 49, 38, 255);

    public static readonly Color Border =
        new Color32(48, 58, 49, 255);

    public static readonly Color Green =
        new Color32(121, 214, 107, 255);

    public static readonly Color GreenDim =
        new Color32(38, 71, 42, 255);

    public static readonly Color Orange =
        new Color32(255, 196, 80, 255);

    public static readonly Color Amber =
        new Color32(242, 169, 0, 255);

    public static readonly Color Red =
        new Color32(239, 91, 100, 255);

    public static readonly Color TextPrimary =
        new Color32(233, 238, 233, 255);

    public static readonly Color TextMuted =
        new Color32(145, 160, 151, 255);

    public static void StyleDarkGreenButton(
        Button button)
    {
        if (button == null)
            return;

        Graphic target =
            button.targetGraphic != null
                ? button.targetGraphic
                : button.GetComponent<Graphic>();

        if (target != null)
            target.color = GreenDim;

        ColorBlock colors = button.colors;

        colors.normalColor = GreenDim;
        colors.highlightedColor =
            Color.Lerp(GreenDim, Green, 0.28f);
        colors.pressedColor =
            Color.Lerp(GreenDim, Background, 0.32f);
        colors.selectedColor = colors.highlightedColor;
        colors.disabledColor = WithAlpha(Control, 0.65f);
        colors.colorMultiplier = 1.0f;

        button.colors = colors;

        foreach (TMP_Text label in
                 button.GetComponentsInChildren<TMP_Text>(true))
        {
            label.color = Color.white;
            label.fontStyle |= FontStyles.Bold;
        }
    }

    public static Color WithAlpha(
        Color color,
        float alpha)
    {
        color.a = Mathf.Clamp01(alpha);
        return color;
    }
}
