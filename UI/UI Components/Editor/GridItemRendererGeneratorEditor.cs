using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Vant.UI.UIComponents;

namespace Vant.UI.UIComponents.Editor
{
    [CustomEditor(typeof(GridItemRendererGenerator), true)]
    public class GridItemRendererGeneratorEditor : UnityEditor.Editor
    {
        private GridItemRendererGenerator _target;
        private bool _useNamespace;

        private void OnEnable()
        {
            _target = (GridItemRendererGenerator)target;
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
            EditorGUILayout.PropertyField(serializedObject.FindProperty("generatePath"), new GUIContent("Generate Path (Relative to Assets)"));
            
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Reference Configuration", EditorStyles.boldLabel);

            if (GUILayout.Button("Add Reference"))
            {
                _target.references.Add(new GridItemRendererGenerator.ReferenceData());
            }

            float maxRefWidth = 100f;
            float maxTypeWidth = 100f;

            foreach (var item in _target.references)
            {
                if (item.reference != null)
                {
                    float nameWidth = GUI.skin.label.CalcSize(new GUIContent(item.reference.name)).x;
                    if (nameWidth + 60 > maxRefWidth) maxRefWidth = nameWidth + 60;

                    if (!string.IsNullOrEmpty(item.typeName))
                    {
                        float typeWidth = EditorStyles.popup.CalcSize(new GUIContent(item.typeName)).x;
                        if (typeWidth > maxTypeWidth) maxTypeWidth = typeWidth;
                    }
                }
            }

            for (int i = 0; i < _target.references.Count; i++)
            {
                DrawReferenceItem(i, maxRefWidth, maxTypeWidth);
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

        private void DrawReferenceItem(int index, float refWidth, float typeWidth)
        {
            if (index >= _target.references.Count) return;
            
            var item = _target.references[index];
            EditorGUILayout.BeginHorizontal("box");
            
            // Alias
            item.alias = EditorGUILayout.TextField(item.alias, GUILayout.Width(120));

            // Reference
            EditorGUI.BeginChangeCheck();
            item.reference = (UnityEngine.Object)EditorGUILayout.ObjectField(item.reference, typeof(UnityEngine.Object), true, GUILayout.Width(refWidth));
            if (EditorGUI.EndChangeCheck())
            {
                if (item.reference != null)
                {
                    if (string.IsNullOrEmpty(item.typeName))
                    {
                        item.typeName = item.reference.GetType().FullName;
                    }
                }
            }

            // Type Selection
            if (item.reference != null)
            {
                var typeNames = new List<string>();
                
                GameObject go = item.reference as GameObject;
                if (go == null && item.reference is Component c)
                {
                    go = c.gameObject;
                }

                if (go != null)
                {
                    typeNames.Add(typeof(GameObject).FullName);
                    var allComps = go.GetComponents<Component>();
                    foreach (var comp in allComps)
                    {
                        if (comp != null) typeNames.Add(comp.GetType().FullName);
                    }
                }
                else
                {
                    typeNames.Add(item.reference.GetType().FullName);
                }

                int selectedIndex = typeNames.IndexOf(item.typeName);
                if (selectedIndex < 0) selectedIndex = 0;

                int newIndex = EditorGUILayout.Popup(selectedIndex, typeNames.ToArray(), GUILayout.Width(typeWidth));
                if (newIndex >= 0 && newIndex < typeNames.Count)
                {
                    item.typeName = typeNames[newIndex];
                }
            }
            else
            {
                EditorGUILayout.LabelField("None", GUILayout.Width(typeWidth));
            }

            if (GUILayout.Button("X", GUILayout.Width(20)))
            {
                _target.references.RemoveAt(index);
            }
            
            EditorGUILayout.EndHorizontal();
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
                if (!string.IsNullOrEmpty(refData.alias) && !string.IsNullOrEmpty(refData.typeName))
                {
                    sb.AppendLine($"{indent}    public {refData.typeName} {refData.alias};");
                }
            }
            
            sb.AppendLine();
            sb.AppendLine($"{indent}    public {_target.skinName}(GameObject gameObject)");
            sb.AppendLine($"{indent}    {{");
            sb.AppendLine($"{indent}        Replace(gameObject);");
            sb.AppendLine($"{indent}    }}");

            sb.AppendLine($"{indent}    public void Replace(GameObject gameObject)");
            sb.AppendLine($"{indent}    {{");
            sb.AppendLine($"{indent}        this.gameObject = gameObject;");
            sb.AppendLine($"{indent}        var generator = gameObject.GetComponent<GridItemRendererGenerator>();");
            
            foreach (var refData in _target.references)
            {
                 if (!string.IsNullOrEmpty(refData.alias) && !string.IsNullOrEmpty(refData.typeName))
                 {
                     sb.AppendLine($"{indent}        {refData.alias} = generator.GetReference<{refData.typeName}>(\"{refData.alias}\");");
                 }
            }
            
            sb.AppendLine($"{indent}    }}");

            sb.AppendLine($"{indent}    public void Dispose()");
            sb.AppendLine($"{indent}    {{");
            sb.AppendLine($"{indent}        gameObject = null;");
            foreach (var refData in _target.references)
            {
                if (!string.IsNullOrEmpty(refData.alias) && !string.IsNullOrEmpty(refData.typeName))
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
