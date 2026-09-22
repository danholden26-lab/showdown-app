using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

[CustomPropertyDrawer(typeof(IChartEffect), true)]
[CustomPropertyDrawer(typeof(IRollModifierEffect), true)]
[CustomPropertyDrawer(typeof(IResultEffect), true)]
[CustomPropertyDrawer(typeof(IStatModifierEffect), true)]
public class SerializeReferenceDropdownDrawer : PropertyDrawer
{
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        if (property.propertyType != SerializedPropertyType.ManagedReference)
        {
            EditorGUI.PropertyField(position, property, label, true);
            return;
        }

        var dropdownRect = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);

        string currentTypeName = property.managedReferenceFullTypename;
        string displayName = string.IsNullOrEmpty(currentTypeName)
            ? "<None>"
            : currentTypeName.Split(' ').Last().Split('.').Last();

        if (EditorGUI.DropdownButton(dropdownRect, new GUIContent($"{label.text}: {displayName}"), FocusType.Keyboard))
        {
            ShowTypeMenu(property);
        }

        // Draw the actual fields of whatever type is currently assigned, below the dropdown
        if (property.managedReferenceValue != null)
        {
            var fieldRect = new Rect(position.x, position.y + EditorGUIUtility.singleLineHeight + 2,
                                      position.width, position.height - EditorGUIUtility.singleLineHeight - 2);
            EditorGUI.PropertyField(fieldRect, property, GUIContent.none, true);
        }
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        if (property.propertyType != SerializedPropertyType.ManagedReference)
            return EditorGUI.GetPropertyHeight(property, label, true);

        float height = EditorGUIUtility.singleLineHeight + 2;
        if (property.managedReferenceValue != null)
            height += EditorGUI.GetPropertyHeight(property, GUIContent.none, true);

        return height;
    }

    private void ShowTypeMenu(SerializedProperty property)
    {
        var menu = new GenericMenu();
        string baseTypeName = property.managedReferenceFieldTypename.Split(' ').Last();

        var baseType = AppDomain.CurrentDomain.GetAssemblies()
            .SelectMany(a => a.GetTypes())
            .FirstOrDefault(t => t.Name == baseTypeName || t.FullName == baseTypeName);

        if (baseType == null) return;

        var concreteTypes = AppDomain.CurrentDomain.GetAssemblies()
            .SelectMany(a => a.GetTypes())
            .Where(t => baseType.IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract)
            .ToList();

        foreach (var type in concreteTypes)
        {
            menu.AddItem(new GUIContent(type.Name), false, () =>
            {
                property.serializedObject.Update();
                property.managedReferenceValue = Activator.CreateInstance(type);
                property.serializedObject.ApplyModifiedProperties();
            });
        }

        menu.ShowAsContext();
    }
}