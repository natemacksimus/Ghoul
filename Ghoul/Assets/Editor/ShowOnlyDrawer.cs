using UnityEditor;
using UnityEngine;

// This class provides the logic for the [ShowOnly] attribute for properties and variables
[CustomPropertyDrawer(typeof(ShowOnlyAttribute))]
public class ShowOnlyDrawer : PropertyDrawer
{
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        // Method 1:

        //GUI.enabled = false;
        //EditorGUI.PropertyField(position, property, label, true);
        //GUI.enabled = true;


        // Method 2:

        string valueStr;

        switch (property.propertyType)
        {
            case SerializedPropertyType.Boolean:
                valueStr = property.boolValue.ToString();
                break;
            case SerializedPropertyType.Integer:
                valueStr = property.intValue.ToString();
                break;
            case SerializedPropertyType.Float:
                valueStr = property.floatValue.ToString();
                break;
            case SerializedPropertyType.String:
                valueStr = property.stringValue.ToString();
                break;
            case SerializedPropertyType.Vector2:
                valueStr = property.vector2Value.ToString();
                break;
            case SerializedPropertyType.Vector3:
                valueStr = property.vector3Value.ToString();
                break;
            default:
                valueStr = "(not supported)";
                break;
        }

        EditorGUI.LabelField(position, label.text, valueStr);
    }

}
