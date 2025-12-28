#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Vant.Editor
{
    public class ModelTemplateGenerator : EditorWindow
    {
        private string _scriptName = "NewVantModel";
        private string _namespaceName = "";
        private string _targetPath = "Assets";

        [MenuItem("Assets/Create/Vant/Model Script", false, 81)]
        [MenuItem("Vant Framework/Code Generator/Model Script")]
        public static void ShowWindow()
        {
            var window = GetWindow<ModelTemplateGenerator>("Create Model Script");
            window._targetPath = GetSelectedPath();
            window.Show();
        }

        private static string GetSelectedPath()
        {
            var obj = Selection.activeObject;
            if (obj == null) return "Assets";

            string path = AssetDatabase.GetAssetPath(obj.GetInstanceID());
            if (string.IsNullOrEmpty(path)) return "Assets";

            if (Directory.Exists(path)) return path;
            return Path.GetDirectoryName(path);
        }

        private void OnGUI()
        {
            GUILayout.Label("Model Script Generator", EditorStyles.boldLabel);

            _scriptName = EditorGUILayout.TextField("Script Name", _scriptName);
            _namespaceName = EditorGUILayout.TextField("Namespace", _namespaceName);
            
            EditorGUILayout.LabelField("Target Path", _targetPath);

            EditorGUILayout.Space();

            if (GUILayout.Button("Create Script"))
            {
                CreateScript();
            }
        }

        private void CreateScript()
        {
            if (string.IsNullOrEmpty(_scriptName))
            {
                EditorUtility.DisplayDialog("Error", "Script Name cannot be empty.", "OK");
                return;
            }

            string fullPath = Path.Combine(Directory.GetParent(Application.dataPath).FullName, _targetPath, _scriptName + ".cs");
            string directory = Path.GetDirectoryName(fullPath);

            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            if (File.Exists(fullPath))
            {
                if (!EditorUtility.DisplayDialog("Warning", $"File {_scriptName}.cs already exists. Overwrite?", "Yes", "No"))
                {
                    return;
                }
            }

            string content = GenerateTemplate(_scriptName, _namespaceName);
            File.WriteAllText(fullPath, content);
            AssetDatabase.Refresh();
            
            Debug.Log($"Model Script created at: {fullPath}");
            
            var obj = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(Path.Combine(_targetPath, _scriptName + ".cs"));
            Selection.activeObject = obj;
            
            Close();
        }

        private string GenerateTemplate(string className, string namespaceName)
        {
            return $@"using System;
using System.Collections.Generic;
using Vant.Core;
using Vant.MVC;

namespace {namespaceName}
{{
    public class {className} : AbstractModelBase
    {{
        #region Lifecycle

        /// <summary>
        /// 注册事件
        /// </summary>
        protected override void RegisterEvents()
        {{
            base.RegisterEvents();
        }}

        /// <summary>
        /// 解绑事件
        /// </summary>
        protected override void UnregisterEvents()
        {{
            base.UnregisterEvents();
        }}

        /// <summary>
        /// 销毁时调用
        /// </summary>
        protected override void OnDispose()
        {{
            base.OnDispose();
        }}

        #endregion
    }}
}}";
        }
    }
}
#endif
