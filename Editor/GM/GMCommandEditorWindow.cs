#if UNITY_EDITOR
using System.Linq;
using UnityEditor;
using UnityEngine;
using Vant.Core;
using Vant.System.GM;
using DateTime = System.DateTime;

namespace Vant.Editor.GM
{
    public class GMCommandEditorWindow : EditorWindow
    {
        [MenuItem("Vant Framework/Debug/GM Command Tool")]
        public static void ShowWindow()
        {
            GetWindow<GMCommandEditorWindow>("Vant GM Tool");
        }

        private int _selectedCommandIndex = 0;
        private string _argsInput = "";
        private Vector2 _scrollPos;
        private string _log = "";

        private void OnGUI()
        {
            if (!Application.isPlaying)
            {
                EditorGUILayout.HelpBox("GM Tool only works in Play Mode.", MessageType.Info);
                return;
            }

            if (AppCore.Instance == null || AppCore.Instance.GMManager == null)
            {
                EditorGUILayout.HelpBox("AppCore or GMManager not initialized.", MessageType.Warning);
                return;
            }

            var gmManager = AppCore.Instance.GMManager;
            var commands = gmManager.AllCommands.ToList();

            if (commands.Count == 0)
            {
                EditorGUILayout.HelpBox("No GM commands registered.", MessageType.Info);
                return;
            }

            // 1. Command Dropdown
            string[] displayOptions = commands.Select(c => $"{c.Name} - {c.Description}").ToArray();
            
            GUILayout.BeginHorizontal();
            GUILayout.Label("Command:", GUILayout.Width(80));
            _selectedCommandIndex = EditorGUILayout.Popup(_selectedCommandIndex, displayOptions);
            GUILayout.EndHorizontal();

            var selectedCmd = commands[_selectedCommandIndex];

            // 2. Arguments Input
            GUILayout.BeginHorizontal();
            GUILayout.Label("Args:", GUILayout.Width(80));
            _argsInput = EditorGUILayout.TextField(_argsInput);
            GUILayout.EndHorizontal();

            // 3. Execute Button
            if (GUILayout.Button("Execute", GUILayout.Height(30)))
            {
                string fullCommand = $"{selectedCmd.Name} {_argsInput}";
                string result = gmManager.Execute(fullCommand);
                _log = $"[{DateTime.Now:HH:mm:ss}] > {fullCommand}\n{result}\n\n" + _log;
                
                // Clear args after execution? Maybe keep for convenience.
            }

            // 4. Log Area
            GUILayout.Space(10);
            GUILayout.Label("Execution Log:");
            _scrollPos = GUILayout.BeginScrollView(_scrollPos, GUILayout.Height(200));
            GUILayout.TextArea(_log);
            GUILayout.EndScrollView();
            
            // 5. Clear Log
            if (GUILayout.Button("Clear Log"))
            {
                _log = "";
            }
        }
    }
}
#endif
