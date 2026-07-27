#if UNITY_EDITOR

using Sirenix.OdinInspector.Editor;
using UnityEditor;
using UnityEngine;

public class GameplayTagOdinDrawer : OdinValueDrawer<GameplayTag>
{
    private static readonly Color LegacyColor = new Color(1f, 0.55f, 0.15f, 1f);
    private static readonly Color InvalidColor = new Color(1f, 0.35f, 0.35f, 1f);

    protected override void DrawPropertyLayout(GUIContent label)
    {
        var value = ValueEntry.SmartValue;
        var domainFilter = ResolveDomainFilter();
        var items = GameplayTagOdinUtility.GetDropdownItems(domainFilter);

        bool isLegacy = value.IsLegacyMissingDomain;
        bool isBroken = GameplayTagOdinUtility.IsBrokenOrLegacy(value, out var brokenReason);
        bool isKnownValid = value.IsValid && !isBroken;

        int currentIndex = 0;

        for (int i = 0; i < items.Count; i++)
        {
            if (items[i].Value.Equals(value))
            {
                currentIndex = i;
                break;
            }
        }

        // Selected value is outside current filter / legacy — still show so it is not silently reset.
        if (currentIndex == 0 && (value.IsValid || isLegacy))
        {
            string display = GameplayTagOdinUtility.GetDisplayName(value);
            items.Insert(1, new Sirenix.OdinInspector.ValueDropdownItem<GameplayTag>(display, value));
            currentIndex = 1;
        }

        string[] names = new string[items.Count];

        for (int i = 0; i < items.Count; i++)
        {
            names[i] = items[i].Text;
        }

        var rect = EditorGUILayout.GetControlRect();

        if (label != null)
        {
            rect = EditorGUI.PrefixLabel(rect, label);
        }

        // One-click fix for unambiguous legacy tags.
        if (isLegacy &&
            GameplayTagLegacyFixup.TryResolveLegacy(
                value.Value,
                value.Mask,
                out var resolved,
                out _,
                out bool ambiguous) &&
            !ambiguous)
        {
            var fixRect = new Rect(rect.xMax - 44f, rect.y, 44f, rect.height);
            rect.xMax -= 48f;

            if (GUI.Button(fixRect, "Fix", EditorStyles.miniButton))
            {
                ValueEntry.SmartValue = resolved;
                return;
            }
        }

        var prevColor = GUI.color;
        if (isLegacy)
            GUI.color = LegacyColor;
        else if (isBroken && !isKnownValid)
            GUI.color = InvalidColor;

        EditorGUI.BeginChangeCheck();

        int newIndex = EditorGUI.Popup(rect, currentIndex, names);

        GUI.color = prevColor;

        if (EditorGUI.EndChangeCheck())
        {
            newIndex = Mathf.Clamp(newIndex, 0, items.Count - 1);
            ValueEntry.SmartValue = items[newIndex].Value;
        }

        if (isBroken && !string.IsNullOrEmpty(brokenReason))
        {
            var helpRect = GUILayoutUtility.GetRect(0, EditorGUIUtility.singleLineHeight * 1.2f);
            EditorGUI.HelpBox(helpRect, brokenReason, MessageType.Warning);
        }
    }

    private GameplayTagDomain? ResolveDomainFilter()
    {
        var attr = Property.GetAttribute<GameplayTagDomainAttribute>();
        if (attr == null)
            return null;

        return attr.Domain;
    }
}

#endif
