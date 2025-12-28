using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Vant.UI.UIComponents;

namespace Vant.UI.UIComponents.Editor
{
    [CustomEditor(typeof(ReferenceContainerGenerator), true)]
    public class ReferenceContainerGeneratorEditor : UnityEditor.Editor
    {
        private ReferenceContainerGenerator _target;
        private bool _useNamespace;

        private void OnEnable()
        {
            _target = (ReferenceContainerGenerator)target;
            _useNamespace = !string.IsNullOrEmpty(_target.namespaceName);
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            
            EditorGUILayout.BeginHorizontal();
            EditorGUI.BeginChangeCheck();
            _useNamespace = EditorGUILayout.ToggleLeft("Use Namespace", _useNamespace, GUILayout.Width(110));
            if (EditorGUI.EndChangeCheck())
            {
                if (!_useNamespace)
                {
                    serializedObject.FindProperty("namespaceName").stringValue = "";
                }
            }

            if (_useNamespace)
            {
                EditorGUILayout.PropertyField(serializedObject.FindProperty("namespaceName"), GUIContent.none);
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.PropertyField(serializedObject.FindProperty("skinName"), new GUIContent("Skin Name"));
            
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PropertyField(serializedObject.FindProperty("generatePath"), new GUIContent("Generate Path (Relative to Assets)"));
            if (GUILayout.Button("...", GUILayout.Width(30)))
            {
                string path = EditorUtility.OpenFolderPanel("Select Generate Path", Application.dataPath, "");
                if (!string.IsNullOrEmpty(path))
                {
                    if (path.StartsWith(Application.dataPath))
                    {
                        path = path.Substring(Application.dataPath.Length + 1);
                        serializedObject.FindProperty("generatePath").stringValue = path;
                    }
                    else
                    {
                        EditorUtility.DisplayDialog("Error", "Please select a folder inside the Assets directory.", "OK");
                    }
                }
            }
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Reference Configuration", EditorStyles.boldLabel);

            if (GUILayout.Button("Add Reference"))
            {
                _target.references.Add(new ReferenceContainerGenerator.ReferenceData());
            }

            for (int i = 0; i < _target.references.Count; i++)
            {
                DrawReferenceItem(i);
            }
            
            if (GUI.changed)
            {
                EditorUtility.SetDirty(_target);
            }

            EditorGUILayout.Space();
            if (GUILayout.Button("Generate Code"))
            {
                GenerateCode();
            }
            
            serializedObject.ApplyModifiedProperties();
        }

        private void DrawReferenceItem(int index)
        {
            if (index >= _target.references.Count) return;
            var item = _target.references[index];

            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.BeginHorizontal();
            
            EditorGUILayout.LabelField("Alias:", GUILayout.Width(40));
            item.alias = EditorGUILayout.TextField(item.alias, GUILayout.Width(200));

            EditorGUILayout.LabelField("Type:", GUILayout.Width(40));
            item.type = (ReferenceContainerGenerator.ReferenceType)EditorGUILayout.EnumPopup(item.type, GUILayout.Width(100));

            if (GUILayout.Button("+", GUILayout.Width(20)))
            {
                _target.references.Insert(index + 1, new ReferenceContainerGenerator.ReferenceData());
            }

            if (GUILayout.Button("X", GUILayout.Width(20)))
            {
                _target.references.RemoveAt(index);
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.EndVertical();
                return;
            }
            EditorGUILayout.EndHorizontal();

            switch (item.type)
            {
                case ReferenceContainerGenerator.ReferenceType.Object:
                    DrawObjectField(item);
                    break;
                case ReferenceContainerGenerator.ReferenceType.ObjectArray:
                    DrawObjectArrayField(index, item);
                    break;
                case ReferenceContainerGenerator.ReferenceType.Int:
                    item.intValue = EditorGUILayout.IntField("Value", item.intValue);
                    break;
                case ReferenceContainerGenerator.ReferenceType.Int2:
                    item.int2Value = EditorGUILayout.Vector2IntField("Value", item.int2Value);
                    break;
                case ReferenceContainerGenerator.ReferenceType.Int3:
                    item.int3Value = EditorGUILayout.Vector3IntField("Value", item.int3Value);
                    break;
                case ReferenceContainerGenerator.ReferenceType.Float:
                    item.floatValue = EditorGUILayout.FloatField("Value", item.floatValue);
                    break;
                case ReferenceContainerGenerator.ReferenceType.Float2:
                    item.float2Value = EditorGUILayout.Vector2Field("Value", item.float2Value);
                    break;
                case ReferenceContainerGenerator.ReferenceType.Float3:
                    item.float3Value = EditorGUILayout.Vector3Field("Value", item.float3Value);
                    break;
                case ReferenceContainerGenerator.ReferenceType.String:
                    item.stringValue = EditorGUILayout.TextField("Value", item.stringValue);
                    break;
                case ReferenceContainerGenerator.ReferenceType.Bool:
                    item.boolValue = EditorGUILayout.Toggle("Value", item.boolValue);
                    break;
            }
            
            EditorGUILayout.EndVertical();
        }

        private void DrawObjectField(ReferenceContainerGenerator.ReferenceData item)
        {
            EditorGUILayout.BeginHorizontal();
            
            EditorGUILayout.LabelField("Ref:", GUILayout.Width(40));
            
            EditorGUI.BeginChangeCheck();
            item.objectValue = (UnityEngine.Object)EditorGUILayout.ObjectField(GUIContent.none, item.objectValue, typeof(UnityEngine.Object), true, GUILayout.Width(200));
            if (EditorGUI.EndChangeCheck())
            {
                if (item.objectValue != null && string.IsNullOrEmpty(item.typeName))
                {
                    item.typeName = item.objectValue.GetType().FullName;
                }
            }
            
            DrawTypeDropdown(item, item.objectValue);
            EditorGUILayout.EndHorizontal();
        }

        private void DrawObjectArrayField(int index, ReferenceContainerGenerator.ReferenceData item)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Element Type:", GUILayout.Width(100));
            
            UnityEngine.Object sample = null;
            if (item.objectArrayValue != null && item.objectArrayValue.Length > 0)
            {
                foreach(var obj in item.objectArrayValue) if(obj != null) { sample = obj; break; }
            }
            
            DrawTypeDropdown(item, sample);
            EditorGUILayout.EndHorizontal();

            var referencesProp = serializedObject.FindProperty("references");
            if (index < referencesProp.arraySize)
            {
                var itemProp = referencesProp.GetArrayElementAtIndex(index);
                var arrayProp = itemProp.FindPropertyRelative("objectArrayValue");
                
                EditorGUI.indentLevel++;
                EditorGUILayout.BeginHorizontal();
                arrayProp.isExpanded = EditorGUILayout.Foldout(arrayProp.isExpanded, "List Content", true);
                int newSize = EditorGUILayout.IntField(arrayProp.arraySize, GUILayout.Width(50));
                if (newSize != arrayProp.arraySize) arrayProp.arraySize = newSize;
                EditorGUILayout.EndHorizontal();

                if (arrayProp.isExpanded)
                {
                    for (int i = 0; i < arrayProp.arraySize; i++)
                    {
                        var elementProp = arrayProp.GetArrayElementAtIndex(i);
                        EditorGUILayout.PropertyField(elementProp, new GUIContent($"Element {i}"));
                    }
                }
                EditorGUI.indentLevel--;
            }
        }

        private void DrawTypeDropdown(ReferenceContainerGenerator.ReferenceData item, UnityEngine.Object obj)
        {
            if (obj != null)
            {
                var typeNames = new List<string>();
                GameObject go = obj as GameObject;
                if (go == null && obj is Component c) go = c.gameObject;

                if (go != null)
                {
                    typeNames.Add(typeof(GameObject).FullName);
                    foreach (var comp in go.GetComponents<Component>())
                    {
                        if (comp != null) typeNames.Add(comp.GetType().FullName);
                    }
                }
                else
                {
                    typeNames.Add(obj.GetType().FullName);
                }

                int selectedIndex = typeNames.IndexOf(item.typeName);
                if (selectedIndex < 0) selectedIndex = 0;

                int newIndex = EditorGUILayout.Popup(selectedIndex, typeNames.ToArray());
                if (newIndex >= 0 && newIndex < typeNames.Count)
                {
                    item.typeName = typeNames[newIndex];
                }
            }
            else
            {
                item.typeName = EditorGUILayout.TextField(item.typeName);
            }
        }

        private void GenerateCode()
        {
            if (string.IsNullOrEmpty(_target.skinName))
            {
                EditorUtility.DisplayDialog("错误", "请指定 Skin Name。", "确定");
                return;
            }

            if (string.IsNullOrEmpty(_target.generatePath))
            {
                EditorUtility.DisplayDialog("错误", "请指定生成路径。", "确定");
                return;
            }

            StringBuilder sb = new StringBuilder();
            sb.AppendLine("using UnityEngine;");
            sb.AppendLine("using UnityEngine.UI;");
            sb.AppendLine("using System.Collections.Generic;");
            sb.AppendLine("using Vant.UI.UIComponents;");
            sb.AppendLine();
            
            bool hasNamespace = !string.IsNullOrEmpty(_target.namespaceName);
            string indent = "";
            
            if (hasNamespace)
            {
                sb.AppendLine($"namespace {_target.namespaceName}");
                sb.AppendLine("{");
                indent = "    ";
            }
            
            sb.AppendLine($"{indent}public class {_target.skinName}");
            sb.AppendLine($"{indent}{{");
            sb.AppendLine($"{indent}    public GameObject gameObject;");
            
            foreach (var refData in _target.references)
            {
                if (string.IsNullOrEmpty(refData.alias)) continue;

                string typeStr = "object";
                
                switch (refData.type)
                {
                    case ReferenceContainerGenerator.ReferenceType.Object:
                        typeStr = string.IsNullOrEmpty(refData.typeName) ? "UnityEngine.Object" : refData.typeName;
                        break;
                    case ReferenceContainerGenerator.ReferenceType.ObjectArray:
                        typeStr = (string.IsNullOrEmpty(refData.typeName) ? "UnityEngine.Object" : refData.typeName) + "[]";
                        break;
                    case ReferenceContainerGenerator.ReferenceType.Int: typeStr = "int"; break;
                    case ReferenceContainerGenerator.ReferenceType.Int2: typeStr = "Vector2Int"; break;
                    case ReferenceContainerGenerator.ReferenceType.Int3: typeStr = "Vector3Int"; break;
                    case ReferenceContainerGenerator.ReferenceType.Float: typeStr = "float"; break;
                    case ReferenceContainerGenerator.ReferenceType.Float2: typeStr = "Vector2"; break;
                    case ReferenceContainerGenerator.ReferenceType.Float3: typeStr = "Vector3"; break;
                    case ReferenceContainerGenerator.ReferenceType.String: typeStr = "string"; break;
                    case ReferenceContainerGenerator.ReferenceType.Bool: typeStr = "bool"; break;
                }
                
                sb.AppendLine($"{indent}    public {typeStr} {refData.alias};");
            }
            
            sb.AppendLine();
            sb.AppendLine($"{indent}    public {_target.skinName}(GameObject gameObject)");
            sb.AppendLine($"{indent}    {{");
            sb.AppendLine($"{indent}        Replace(gameObject);");
            sb.AppendLine($"{indent}    }}");

            sb.AppendLine($"{indent}    public void Replace(GameObject gameObject)");
            sb.AppendLine($"{indent}    {{");
            sb.AppendLine($"{indent}        this.gameObject = gameObject;");
            sb.AppendLine($"{indent}        var generator = gameObject.GetComponent<ReferenceContainerGenerator>();");
            
            foreach (var refData in _target.references)
            {
                 if (string.IsNullOrEmpty(refData.alias)) continue;

                 string genericArg = "";
                 switch (refData.type)
                 {
                    case ReferenceContainerGenerator.ReferenceType.Object:
                        genericArg = $"<{ (string.IsNullOrEmpty(refData.typeName) ? "UnityEngine.Object" : refData.typeName) }>";
                        break;
                    case ReferenceContainerGenerator.ReferenceType.ObjectArray:
                        genericArg = $"<{ (string.IsNullOrEmpty(refData.typeName) ? "UnityEngine.Object" : refData.typeName) }[]>";
                        break;
                    case ReferenceContainerGenerator.ReferenceType.Int: genericArg = "<int>"; break;
                    case ReferenceContainerGenerator.ReferenceType.Int2: genericArg = "<Vector2Int>"; break;
                    case ReferenceContainerGenerator.ReferenceType.Int3: genericArg = "<Vector3Int>"; break;
                    case ReferenceContainerGenerator.ReferenceType.Float: genericArg = "<float>"; break;
                    case ReferenceContainerGenerator.ReferenceType.Float2: genericArg = "<Vector2>"; break;
                    case ReferenceContainerGenerator.ReferenceType.Float3: genericArg = "<Vector3>"; break;
                    case ReferenceContainerGenerator.ReferenceType.String: genericArg = "<string>"; break;
                    case ReferenceContainerGenerator.ReferenceType.Bool: genericArg = "<bool>"; break;
                 }

                 sb.AppendLine($"{indent}        {refData.alias} = generator.Get{genericArg}(\"{refData.alias}\");");
            }
            
            sb.AppendLine($"{indent}    }}");

            sb.AppendLine($"{indent}    public void Dispose()");
            sb.AppendLine($"{indent}    {{");
            sb.AppendLine($"{indent}        gameObject = null;");
            foreach (var refData in _target.references)
            {
                if (string.IsNullOrEmpty(refData.alias)) continue;
                
                if (refData.type == ReferenceContainerGenerator.ReferenceType.Object || 
                    refData.type == ReferenceContainerGenerator.ReferenceType.ObjectArray ||
                    refData.type == ReferenceContainerGenerator.ReferenceType.String)
                {
                    sb.AppendLine($"{indent}        {refData.alias} = null;");
                }
            }
            sb.AppendLine($"{indent}    }}");

            sb.AppendLine($"{indent}}}");
            
            if (hasNamespace)
            {
                sb.AppendLine("}");
            }

            string fullPath = Path.Combine(Application.dataPath, _target.generatePath);
            if (!Directory.Exists(fullPath))
            {
                Directory.CreateDirectory(fullPath);
            }
            
            string filePath = Path.Combine(fullPath, _target.skinName + ".cs");
            File.WriteAllText(filePath, sb.ToString());
            AssetDatabase.Refresh();
            Debug.Log($"代码已生成至: {filePath}");
        }
    }
}
