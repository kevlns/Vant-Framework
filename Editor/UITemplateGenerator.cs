#if UNITY_EDITOR
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Vant.Editor
{
    public class UITemplateGenerator : EditorWindow
    {
        private const string NamespacePrefKey = "Vant.Editor.UITemplateGenerator.Namespace";
        private string _scriptName = "NewVantUI";
        private string _namespaceName = "";
        private string _targetPath = "Assets";

        [MenuItem("Assets/Create/Vant/UI Script", false, 80)]
        [MenuItem("Vant Framework/Code Generator/UI Script")]
        public static void ShowWindow()
        {
            var window = GetWindow<UITemplateGenerator>("Create UI Script");
            window._targetPath = GetSelectedPath();
            window._namespaceName = EditorPrefs.GetString(NamespacePrefKey, "");
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
            EditorPrefs.SetString(NamespacePrefKey, _namespaceName);

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
            string constName = ToUpperSnake(className);
            var sb = new StringBuilder();
            sb.AppendLine("using System;");
            sb.AppendLine("using System.Collections.Generic;");
            sb.AppendLine("using UnityEngine;");
            sb.AppendLine("using UnityEngine.UI;");
            sb.AppendLine("using Cysharp.Threading.Tasks;");
            sb.AppendLine("using Vant.Core;");
            sb.AppendLine("using Vant.MVC;");
            sb.AppendLine("using Vant.UI.UIFramework;");
            sb.AppendLine();

            bool hasNamespace = !string.IsNullOrWhiteSpace(namespaceName);
            if (hasNamespace)
            {
                sb.AppendLine($"namespace {namespaceName}");
                sb.AppendLine("{");
            }

            string indent = hasNamespace ? "    " : string.Empty;

            sb.AppendLine($"{indent}/// <summary>");
            sb.AppendLine($"{indent}/// UI 打开事件参数定义");
            sb.AppendLine($"{indent}/// </summary>");
            sb.AppendLine($"{indent}public static partial class UIName");
            sb.AppendLine($"{indent}{{");
            sb.AppendLine($"{indent}    /// <summary>");
            sb.AppendLine($"{indent}    /// UI 名称常量（{className} -> {constName}）");
            sb.AppendLine($"{indent}    /// </summary>");
            sb.AppendLine($"{indent}    public const string {constName} = \"{className}\";");
            sb.AppendLine($"{indent}}}");
            sb.AppendLine();

            sb.AppendLine($"{indent}public class {className} : AbstractUIBase");
            sb.AppendLine($"{indent}{{");
            sb.AppendLine();
            sb.AppendLine($"{indent}    #region UI Configuration");
            sb.AppendLine();
            sb.AppendLine($"{indent}    public override UIConfig RegisterConfig => StaticConfig;");
            sb.AppendLine($"{indent}    public static readonly UIConfig StaticConfig = new UIConfig");
            sb.AppendLine($"{indent}    {{");
            sb.AppendLine($"{indent}        Name = \"{className}\",");
            sb.AppendLine($"{indent}        AssetPath = \"\",");
            sb.AppendLine($"{indent}        UIClass = typeof({className}),");
            sb.AppendLine($"{indent}        Layer = UILayer.Normal,");
            sb.AppendLine($"{indent}        Mode = UIMode.Overlay,");
            sb.AppendLine($"{indent}        NeedMask = false,");
            sb.AppendLine($"{indent}        IsCacheable = true,");
            sb.AppendLine($"{indent}        AllowMultiInstance = false,");
            sb.AppendLine($"{indent}        EnterAnimation = null,");
            sb.AppendLine($"{indent}        ExitAnimation = null,");
            sb.AppendLine($"{indent}    }};");
            sb.AppendLine();
            sb.AppendLine($"{indent}    #endregion");
            sb.AppendLine();
            sb.AppendLine($"{indent}    #region Lifecycle");
            sb.AppendLine();
            sb.AppendLine($"{indent}    /// <summary>");
            sb.AppendLine($"{indent}    /// 创建时调用 (只调用一次)");
            sb.AppendLine($"{indent}    /// 用于初始化组件引用、事件监听等");
            sb.AppendLine($"{indent}    /// </summary>");
            sb.AppendLine($"{indent}    protected override void OnCreate()");
            sb.AppendLine($"{indent}    {{");
            sb.AppendLine($"{indent}        base.OnCreate();");
            sb.AppendLine($"{indent}    }}");
            sb.AppendLine();
            sb.AppendLine($"{indent}    /// <summary>");
            sb.AppendLine($"{indent}    /// 打开前调用");
            sb.AppendLine($"{indent}    /// 用于重置状态、准备数据。支持异步。");
            sb.AppendLine($"{indent}    /// </summary>");
            sb.AppendLine($"{indent}    protected override async UniTask OnBeforeOpen(object args)");
            sb.AppendLine($"{indent}    {{");
            sb.AppendLine($"{indent}        await base.OnBeforeOpen(args);");
            sb.AppendLine($"{indent}    }}");
            sb.AppendLine();
            sb.AppendLine($"{indent}    /// <summary>");
            sb.AppendLine($"{indent}    /// 打开UI时立即刷新一次");
            sb.AppendLine($"{indent}    /// 用于将数据绑定到 UI 元素");
            sb.AppendLine($"{indent}    /// </summary>");
            sb.AppendLine($"{indent}    protected override void OnRefreshOnceOnOpen()");
            sb.AppendLine($"{indent}    {{");
            sb.AppendLine($"{indent}        base.OnRefreshOnceOnOpen();");
            sb.AppendLine($"{indent}    }}");
            sb.AppendLine();
            sb.AppendLine($"{indent}    /// <summary>");
            sb.AppendLine($"{indent}    /// 打开后调用 (动画播放完毕后)");
            sb.AppendLine($"{indent}    /// </summary>");
            sb.AppendLine($"{indent}    protected override async UniTask OnAfterOpen()");
            sb.AppendLine($"{indent}    {{");
            sb.AppendLine($"{indent}        await base.OnAfterOpen();");
            sb.AppendLine($"{indent}    }}");
            sb.AppendLine();
            sb.AppendLine($"{indent}    /// <summary>");
            sb.AppendLine($"{indent}    /// 关闭前调用");
            sb.AppendLine($"{indent}    /// </summary>");
            sb.AppendLine($"{indent}    protected override async UniTask OnBeforeClose()");
            sb.AppendLine($"{indent}    {{");
            sb.AppendLine($"{indent}        await base.OnBeforeClose();");
            sb.AppendLine($"{indent}    }}");
            sb.AppendLine();
            sb.AppendLine($"{indent}    /// <summary>");
            sb.AppendLine($"{indent}    /// 销毁时调用");
            sb.AppendLine($"{indent}    /// </summary>");
            sb.AppendLine($"{indent}    protected override void OnDestroyUI()");
            sb.AppendLine($"{indent}    {{");
            sb.AppendLine($"{indent}        base.OnDestroyUI();");
            sb.AppendLine($"{indent}    }}");
            sb.AppendLine();
            sb.AppendLine($"{indent}    #endregion");
            sb.AppendLine($"{indent}}}");

            if (hasNamespace)
            {
                sb.AppendLine("}");
            }

            return sb.ToString();
        }

        private static string ToUpperSnake(string input)
        {
            if (string.IsNullOrEmpty(input)) return input;

            var sb = new StringBuilder();
            for (int i = 0; i < input.Length; i++)
            {
                char c = input[i];
                if (char.IsUpper(c) && i > 0)
                {
                    sb.Append('_');
                }
                sb.Append(char.ToUpperInvariant(c));
            }
            return sb.ToString();
        }
    }
}
#endif
