#if UNITY_EDITOR
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Vant.Editor
{
    public class MVCTemplateGenerator : EditorWindow
    {
        private const string NamespacePrefKey = "Vant.Editor.MVCTemplateGenerator.Namespace";
        private string _scriptName = "NewMVCComponent";
        private string _namespaceName = "";
        private string _targetPath = "Assets";
        private GenerationType _generationType = GenerationType.Entity;

        private enum GenerationType
        {
            GeneralView,
            Controller,
            Entity
        }

        [MenuItem("Assets/Create/Vant/MVC Entity Bundle", false, 83)]
        [MenuItem("Vant Framework/Code Generator/MVC Entity Bundle")]
        public static void ShowEntityWindow()
        {
            var window = GetWindow<MVCTemplateGenerator>("Create MVC Entity Bundle");
            window._generationType = GenerationType.Entity;
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
            GUILayout.Label("MVC Component Generator", EditorStyles.boldLabel);
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

            if (_generationType == GenerationType.Entity)
            {
                CreateEntityBundle();
            }
            else
            {
                string content = GenerateTemplate(_scriptName, _namespaceName, _generationType);
                File.WriteAllText(fullPath, content);
                AssetDatabase.Refresh();

                Debug.Log($"MVC Component script created at: {fullPath}");

                var obj = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(Path.Combine(_targetPath, _scriptName + ".cs"));
                Selection.activeObject = obj;

                Close();
            }
        }

        private void CreateEntityBundle()
        {
            if (string.IsNullOrEmpty(_scriptName))
            {
                EditorUtility.DisplayDialog("Error", "Entity Name cannot be empty.", "OK");
                return;
            }

            string entityName = _scriptName;
            string controllerName = $"{_scriptName}Controller";
            string viewName = $"{_scriptName}View";
            string viewModelName = $"{_scriptName}ViewModel";

            string basePath = Path.Combine(Directory.GetParent(Application.dataPath).FullName, _targetPath);
            if (!Directory.Exists(basePath))
            {
                Directory.CreateDirectory(basePath);
            }

            string entityPath = Path.Combine(basePath, entityName + ".cs");
            string controllerPath = Path.Combine(basePath, controllerName + ".cs");
            string viewPath = Path.Combine(basePath, viewName + ".cs");
            string viewModelPath = Path.Combine(basePath, viewModelName + ".cs");

            bool hasAny = File.Exists(entityPath) || File.Exists(controllerPath) || File.Exists(viewPath) || File.Exists(viewModelPath);
            if (hasAny)
            {
                if (!EditorUtility.DisplayDialog("Warning", "One or more files already exist. Overwrite all?", "Yes", "No"))
                {
                    return;
                }
            }

            File.WriteAllText(entityPath, GenerateEntityTemplate(entityName, controllerName, viewName, viewModelName, _namespaceName));
            File.WriteAllText(controllerPath, GenerateControllerTemplate(controllerName, viewName, viewModelName, _namespaceName));
            File.WriteAllText(viewPath, GenerateViewTemplate(viewName, _namespaceName));
            File.WriteAllText(viewModelPath, GenerateViewModelTemplate(viewModelName, _namespaceName));

            AssetDatabase.Refresh();

            Debug.Log($"MVC Entity bundle created at: {basePath}");

            var obj = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(Path.Combine(_targetPath, entityName + ".cs"));
            Selection.activeObject = obj;

            Close();
        }

        private string GenerateTemplate(string className, string namespaceName, GenerationType generationType)
        {
            var sb = new StringBuilder();
            sb.AppendLine("using UnityEngine;");
            sb.AppendLine("using Cysharp.Threading.Tasks;");
            sb.AppendLine("using Vant.MVC;");
            sb.AppendLine();

            bool hasNamespace = !string.IsNullOrWhiteSpace(namespaceName);
            if (hasNamespace)
            {
                sb.AppendLine($"namespace {namespaceName}");
                sb.AppendLine("{");
            }

            string indent = hasNamespace ? "    " : string.Empty;

            if (generationType == GenerationType.GeneralView)
            {
                // Generate General View template
                sb.AppendLine($"{indent}public class {className} : AbstractGeneralViewBase");
                sb.AppendLine($"{indent}{{");
                sb.AppendLine();
                sb.AppendLine($"{indent}    #region View Configuration");
                sb.AppendLine();
                sb.AppendLine($"{indent}    public override GeneralViewConfig RegisterConfig => StaticConfig;");
                sb.AppendLine($"{indent}    public static readonly GeneralViewConfig StaticConfig = new GeneralViewConfig");
                sb.AppendLine($"{indent}    {{");
                sb.AppendLine($"{indent}        Name = \"{className}\",");
                sb.AppendLine($"{indent}        PrefabPath = \"\",");
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
                sb.AppendLine($"{indent}    }}");
                sb.AppendLine();
                sb.AppendLine($"{indent}    /// <summary>");
                sb.AppendLine($"{indent}    /// 打开前调用");
                sb.AppendLine($"{indent}    /// 用于重置状态、准备数据。支持异步。");
                sb.AppendLine($"{indent}    /// </summary>");
                sb.AppendLine($"{indent}    protected override async UniTask OnBeforeShow(object args)");
                sb.AppendLine($"{indent}    {{");
                sb.AppendLine($"{indent}    }}");
                sb.AppendLine();
                sb.AppendLine($"{indent}    /// <summary>");
                sb.AppendLine($"{indent}    /// 打开UI时立即刷新一次");
                sb.AppendLine($"{indent}    /// 用于将数据绑定到 UI 元素");
                sb.AppendLine($"{indent}    /// </summary>");
                sb.AppendLine($"{indent}    protected override void OnRefreshOnceOnOpen()");
                sb.AppendLine($"{indent}    {{");
                sb.AppendLine($"{indent}    }}");
                sb.AppendLine();
                sb.AppendLine($"{indent}    /// <summary>");
                sb.AppendLine($"{indent}    /// 打开后调用 (动画播放完毕后)");
                sb.AppendLine($"{indent}    /// </summary>");
                sb.AppendLine($"{indent}    protected override async UniTask OnAfterShow()");
                sb.AppendLine($"{indent}    {{");
                sb.AppendLine($"{indent}    }}");
                sb.AppendLine();
                sb.AppendLine($"{indent}    /// <summary>");
                sb.AppendLine($"{indent}    /// 关闭时调用");
                sb.AppendLine($"{indent}    /// </summary>");
                sb.AppendLine($"{indent}    protected override void OnHide()");
                sb.AppendLine($"{indent}    {{");
                sb.AppendLine($"{indent}    }}");
                sb.AppendLine();
                sb.AppendLine($"{indent}    /// <summary>");
                sb.AppendLine($"{indent}    /// 销毁时调用");
                sb.AppendLine($"{indent}    /// </summary>");
                sb.AppendLine($"{indent}    protected override void OnDestroy()");
                sb.AppendLine($"{indent}    {{");
                sb.AppendLine($"{indent}        Notifier?.RemoveAllListeners(this);");
                sb.AppendLine($"{indent}    }}");
                sb.AppendLine();
                sb.AppendLine($"{indent}    #endregion");
                sb.AppendLine($"{indent}}}");
            }
            else if (generationType == GenerationType.Controller)
            {
                // Generate Controller template - only lifecycle methods
                sb.AppendLine($"{indent}public class {className} : AbstractControllerBase<AbstractGeneralViewBase, IViewModel>");
                sb.AppendLine($"{indent}{{");
                sb.AppendLine();
                sb.AppendLine($"{indent}    #region Lifecycle");
                sb.AppendLine();
                sb.AppendLine($"{indent}    /// <summary>");
                sb.AppendLine($"{indent}    /// 创建时调用 (只调用一次)");
                sb.AppendLine($"{indent}    /// </summary>");
                sb.AppendLine($"{indent}    protected override void OnInit()");
                sb.AppendLine($"{indent}    {{");
                sb.AppendLine($"{indent}        base.OnInit();");
                sb.AppendLine($"{indent}    }}");
                sb.AppendLine();
                sb.AppendLine($"{indent}    /// <summary>");
                sb.AppendLine($"{indent}    /// 每帧刷新时调用");
                sb.AppendLine($"{indent}    /// </summary>");
                sb.AppendLine($"{indent}    protected override void OnUpdate(float deltaTime, float unscaledDeltaTime = 0)");
                sb.AppendLine($"{indent}    {{");
                sb.AppendLine($"{indent}        base.OnUpdate(deltaTime, unscaledDeltaTime);");
                sb.AppendLine($"{indent}    }}");
                sb.AppendLine();
                sb.AppendLine($"{indent}    /// <summary>");
                sb.AppendLine($"{indent}    /// 销毁时调用 (只调用一次)");
                sb.AppendLine($"{indent}    /// </summary>");
                sb.AppendLine($"{indent}    protected override void OnDestroy()");
                sb.AppendLine($"{indent}    {{");
                sb.AppendLine($"{indent}        base.OnDestroy();");
                sb.AppendLine($"{indent}    }}");
                sb.AppendLine();
                sb.AppendLine($"{indent}    #endregion");
                sb.AppendLine($"{indent}}}");
            }

            if (hasNamespace)
            {
                sb.AppendLine("}");
            }

            return sb.ToString();
        }

        private string GenerateEntityTemplate(string entityName, string controllerName, string viewName, string viewModelName, string namespaceName)
        {
            var sb = new StringBuilder();
            sb.AppendLine("using Cysharp.Threading.Tasks;");
            sb.AppendLine("using Vant.MVC;");
            sb.AppendLine();

            bool hasNamespace = !string.IsNullOrWhiteSpace(namespaceName);
            if (hasNamespace)
            {
                sb.AppendLine($"namespace {namespaceName}");
                sb.AppendLine("{");
            }

            string indent = hasNamespace ? "    " : string.Empty;

            sb.AppendLine($"{indent}public class {entityName} : AbstractMVCEntityBase<{controllerName}, {viewName}, {viewModelName}>");
            sb.AppendLine($"{indent}{{");
            sb.AppendLine($"{indent}    public {entityName}({controllerName} controller, {viewName} view, {viewModelName} viewModel, bool showOnInit) : base(controller, view, viewModel, showOnInit)");
            sb.AppendLine($"{indent}    {{");
            sb.AppendLine($"{indent}    }}");
            sb.AppendLine($"{indent}}}");

            if (hasNamespace)
            {
                sb.AppendLine("}");
            }

            return sb.ToString();
        }

        private string GenerateControllerTemplate(string className, string namespaceName)
        {
            return GenerateTemplate(className, namespaceName, GenerationType.Controller);
        }

        private string GenerateControllerTemplate(string className, string viewName, string viewModelName, string namespaceName)
        {
            var sb = new StringBuilder();
            sb.AppendLine("using Vant.MVC;");
            sb.AppendLine();

            bool hasNamespace = !string.IsNullOrWhiteSpace(namespaceName);
            if (hasNamespace)
            {
                sb.AppendLine($"namespace {namespaceName}");
                sb.AppendLine("{");
            }

            string indent = hasNamespace ? "    " : string.Empty;

            sb.AppendLine($"{indent}public class {className} : AbstractControllerBase<{viewName}, {viewModelName}>");
            sb.AppendLine($"{indent}{{");
            sb.AppendLine();
            sb.AppendLine($"{indent}    #region Lifecycle");
            sb.AppendLine();
            sb.AppendLine($"{indent}    /// <summary>");
            sb.AppendLine($"{indent}    /// 创建时调用 (只调用一次)");
            sb.AppendLine($"{indent}    /// </summary>");
            sb.AppendLine($"{indent}    protected override void OnInit()");
            sb.AppendLine($"{indent}    {{");
            sb.AppendLine($"{indent}    }}");
            sb.AppendLine();
            sb.AppendLine($"{indent}    /// <summary>");
            sb.AppendLine($"{indent}    /// 每帧刷新时调用");
            sb.AppendLine($"{indent}    /// </summary>");
            sb.AppendLine($"{indent}    protected override void OnUpdate(float deltaTime, float unscaledDeltaTime = 0)");
            sb.AppendLine($"{indent}    {{");
            sb.AppendLine($"{indent}    }}");
            sb.AppendLine();
            sb.AppendLine($"{indent}    /// <summary>");
            sb.AppendLine($"{indent}    /// 销毁时调用 (只调用一次)");
            sb.AppendLine($"{indent}    /// </summary>");
            sb.AppendLine($"{indent}    protected override void OnDestroy()");
            sb.AppendLine($"{indent}    {{");
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

        private string GenerateViewTemplate(string className, string namespaceName)
        {
            return GenerateTemplate(className, namespaceName, GenerationType.GeneralView);
        }

        private string GenerateViewModelTemplate(string className, string namespaceName)
        {
            var sb = new StringBuilder();
            sb.AppendLine("using Vant.MVC;");
            sb.AppendLine();

            bool hasNamespace = !string.IsNullOrWhiteSpace(namespaceName);
            if (hasNamespace)
            {
                sb.AppendLine($"namespace {namespaceName}");
                sb.AppendLine("{");
            }

            string indent = hasNamespace ? "    " : string.Empty;

            sb.AppendLine($"{indent}public class {className} : AbstractViewModelBase");
            sb.AppendLine($"{indent}{{");
            sb.AppendLine($"{indent}    public override void BindProperties()");
            sb.AppendLine($"{indent}    {{");
            sb.AppendLine($"{indent}    }}");
            sb.AppendLine();
            sb.AppendLine($"{indent}    public override void Destroy()");
            sb.AppendLine($"{indent}    {{");
            sb.AppendLine($"{indent}    }}");
            sb.AppendLine($"{indent}}}");

            if (hasNamespace)
            {
                sb.AppendLine("}");
            }

            return sb.ToString();
        }
    }
}
#endif