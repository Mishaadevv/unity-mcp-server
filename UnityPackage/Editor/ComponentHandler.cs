// Component add/inspect/set-property via SerializedObject + reflection fallback.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace UnityMCP.Editor
{
    internal static class ComponentHandler
    {
        public static List<Dictionary<string, object>> GetComponents(GameObject go)
        {
            var list = new List<Dictionary<string, object>>();
            foreach (var c in go.GetComponents<Component>())
            {
                if (c == null) { list.Add(new Dictionary<string, object> { ["type"] = "MissingComponent" }); continue; }
                var d = new Dictionary<string, object> { ["type"] = c.GetType().Name };
                if (c is Behaviour b) d["enabled"] = b.enabled;
                list.Add(d);
            }
            return list;
        }

        public static Component AddComponent(GameObject go, string componentType)
        {
            var type = ResolveComponentType(componentType);
            if (type == null)
                throw new ArgumentException(
                    $"Component type '{componentType}' not found. Use Unity class names like 'Rigidbody', 'BoxCollider', 'Light'.");
            var comp = Undo.AddComponent(go, type);
            EditorUtility.SetDirty(go);
            return comp;
        }

        public static void SetProperty(GameObject go, string componentType, string property, JToken value)
        {
            if (string.IsNullOrEmpty(property))
                throw new ArgumentException("property is required.");

            Component comp = string.IsNullOrEmpty(componentType) || componentType == "GameObject"
                ? (Component)null
                : go.GetComponent(componentType);

            // Special-case GameObject fields
            if (comp == null)
            {
                if (property == "name") { Undo.RecordObject(go, "MCP Set name"); go.name = value.ToObject<string>(); return; }
                if (property == "tag") { Undo.RecordObject(go, "MCP Set tag"); go.tag = value.ToObject<string>(); return; }
                if (property == "active") { Undo.RecordObject(go, "MCP Set active"); go.SetActive(value.ToObject<bool>()); return; }
                if (componentType != null && componentType != "GameObject")
                    throw new ArgumentException($"Component '{componentType}' not found on '{go.name}'. Check unity_get_components first.");
                throw new ArgumentException($"Property '{property}' not supported on GameObject (use name, tag, active).");
            }

            // 1) Try SerializedObject (handles Undo + dirty + Unity types)
            var so = new SerializedObject(comp);
            var sp = so.FindProperty(property);
            if (sp != null)
            {
                Undo.RecordObject(comp, $"MCP Set {componentType}.{property}");
                if (!ApplySerialized(sp, value))
                    throw new ArgumentException(
                        $"Property '{property}' has unsupported type '{sp.propertyType}'. Supported: float, int, bool, string, Vector2/3/4, Color, enum, Object reference by name is not supported in MVP.");
                so.ApplyModifiedProperties();
                EditorUtility.SetDirty(comp);
                return;
            }

            // 2) Reflection fallback for plain fields/properties
            var t = comp.GetType();
            var field = t.GetField(property, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (field != null)
            {
                Undo.RecordObject(comp, $"MCP Set {componentType}.{property}");
                field.SetValue(comp, ConvertToken(value, field.FieldType, property));
                EditorUtility.SetDirty(comp);
                return;
            }
            var prop = t.GetProperty(property, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (prop != null && prop.CanWrite)
            {
                Undo.RecordObject(comp, $"MCP Set {componentType}.{property}");
                prop.SetValue(comp, ConvertToken(value, prop.PropertyType, property), null);
                EditorUtility.SetDirty(comp);
                return;
            }

            throw new ArgumentException(
                $"Property '{property}' not found on '{componentType}'. Use unity_get_object_info to see components, then a valid serialized field.");
        }

        private static bool ApplySerialized(SerializedProperty sp, JToken value)
        {
            switch (sp.propertyType)
            {
                case SerializedPropertyType.Float:
                    sp.floatValue = value.ToObject<float>(); return true;
                case SerializedPropertyType.Integer:
                    sp.intValue = value.ToObject<int>(); return true;
                case SerializedPropertyType.Boolean:
                    sp.boolValue = value.ToObject<bool>(); return true;
                case SerializedPropertyType.String:
                    sp.stringValue = value.ToObject<string>(); return true;
                case SerializedPropertyType.Color:
                    var c = value.ToObject<float[]>();
                    sp.colorValue = new Color(c[0], c[1], c[2], c.Length > 3 ? c[3] : 1f); return true;
                case SerializedPropertyType.Vector2:
                    var v2 = value.ToObject<float[]>(); sp.vector2Value = new Vector2(v2[0], v2[1]); return true;
                case SerializedPropertyType.Vector3:
                    var v3 = value.ToObject<float[]>(); sp.vector3Value = new Vector3(v3[0], v3[1], v3[2]); return true;
                case SerializedPropertyType.Vector4:
                    var v4 = value.ToObject<float[]>(); sp.vector4Value = new Vector4(v4[0], v4[1], v4[2], v4[3]); return true;
                case SerializedPropertyType.Enum:
                    if (value.Type == JTokenType.Integer) sp.enumValueIndex = value.ToObject<int>();
                    else if (value.Type == JTokenType.String)
                        sp.enumValueIndex = Math.Max(0, Array.IndexOf(sp.enumNames, value.ToObject<string>()));
                    else throw new ArgumentException($"Enum '{sp.name}' needs a string name or int index.");
                    return true;
                default:
                    return false;
            }
        }

        private static object ConvertToken(JToken value, Type target, string property)
        {
            try
            {
                if (target == typeof(string)) return value.ToObject<string>();
                if (target == typeof(bool)) return value.ToObject<bool>();
                if (target == typeof(int)) return value.ToObject<int>();
                if (target == typeof(float)) return value.ToObject<float>();
                if (target == typeof(double)) return value.ToObject<double>();
                if (target == typeof(Vector2)) { var a = value.ToObject<float[]>(); return new Vector2(a[0], a[1]); }
                if (target == typeof(Vector3)) { var a = value.ToObject<float[]>(); return new Vector3(a[0], a[1], a[2]); }
                if (target == typeof(Vector4)) { var a = value.ToObject<float[]>(); return new Vector4(a[0], a[1], a[2], a[3]); }
                if (target == typeof(Color)) { var a = value.ToObject<float[]>(); return new Color(a[0], a[1], a[2], a.Length > 3 ? a[3] : 1f); }
                if (target.IsEnum)
                {
                    if (value.Type == JTokenType.String) return Enum.Parse(target, value.ToObject<string>());
                    return Enum.ToObject(target, value.ToObject<int>());
                }
                return value.ToObject(target);
            }
            catch (Exception ex)
            {
                throw new ArgumentException($"Cannot convert value to {target.Name} for '{property}': {ex.Message}");
            }
        }

        private static Type ResolveComponentType(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            // Direct + UnityEngine-qualified lookup first
            var direct = Type.GetType(name) ?? Type.GetType($"UnityEngine.{name}, UnityEngine");
            if (direct != null && typeof(Component).IsAssignableFrom(direct)) return direct;

            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type found = null;
                try { found = asm.GetType(name); } catch { /* skip dynamic assemblies */ }
                if (found != null && typeof(Component).IsAssignableFrom(found)) return found;
                try
                {
                    found = asm.GetTypes().FirstOrDefault(t =>
                        t.Name == name && typeof(Component).IsAssignableFrom(t));
                }
                catch { /* reflection-only assemblies */ }
                if (found != null) return found;
            }
            return null;
        }
    }
}
