#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Vant.Editor
{
    public class UITemplateGenerator : EditorWindow
    {
        private string _scriptName = "NewVantUI";
        private string _namespaceName = "";
        private string _targetPath = "Assets";

        [MenuItem("Assets/Create/Vant/UI Script", false, 80)]
        [MenuItem("Vant Framework/Code Generator/UI Script")]
        public static void ShowWindow()
        {
            var window = GetWindow<UITemplateGenerator>("Create UI Script");
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
            GUILayout.Label("UI Script Generator", EditorStyles.boldLabel);

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

            Debug.Log($"UI Script created at: {fullPath}");

            var obj = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(Path.Combine(_targetPath, _scriptName + ".cs"));
            Selection.activeObject = obj;

            Close();
        }

        private string GenerateTemplate(string className, string namespaceName)
        {
            return $@"using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Cysharp.Threading.Tasks;
using Vant.Core;
using Vant.MVC;
using Vant.UI.UIFramework;

namespace {namespaceName}
{{
    public class {className} : AbstractUIBase
    {{

        #region UI Configuration

        public override UIConfig RegisterConfig => StaticConfig;
        public static readonly UIConfig StaticConfig = new UIConfig
        {{
            Name = ""{className}"",
            AssetPath = """",
            UIClass = typeof({className}),
            Layer = UILayer.Normal,
            Mode = UIMode.Overlay,
            NeedMask = false,
            IsCacheable = true,
            AllowMultiInstance = false,
            EnterAnimation = null,
            ExitAnimation = null,
        }};

        #endregion

        #region Lifecycle

        /// <summary>
        /// 1. 创建时调用 (只调用一次)
        /// 用于初始化组件引用、事件监听等
        /// </summary>
        protected override void OnCreate()
        {{
            base.OnCreate();
        }}

        /// <summary>
        /// 2. 打开前调用
        /// 用于重置状态、准备数据。支持异步。
        /// </summary>
        protected override async UniTask OnBeforeOpen(object args)
        {{
            await base.OnBeforeOpen(args);
        }}

        /// <summary>
        /// 3. 刷新时调用
        /// 用于将数据绑定到 UI 元素
        /// </summary>
        protected override void OnRefresh()
        {{
            base.OnRefresh();
        }}

        /// <summary>
        /// 4. 打开后调用 (动画播放完毕后)
        /// </summary>
        protected override async UniTask OnAfterOpen()
        {{
            await base.OnAfterOpen();
        }}

        /// <summary>
        /// 5. 关闭前调用
        /// </summary>
        protected override async UniTask OnBeforeClose()
        {{
            await base.OnBeforeClose();
        }}

        /// <summary>
        /// 7. 销毁时调用
        /// </summary>
        protected override void OnDestroyUI()
        {{
            base.OnDestroyUI();
        }}

        #endregion
    }}
}}";
        }
    }
}
#endif
