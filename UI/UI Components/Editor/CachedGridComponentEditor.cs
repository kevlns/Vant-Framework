using UnityEngine;
using UnityEditor;
using System;
using System.Reflection;
using System.Linq;
using System.Collections.Generic;
using Vant.UI.UIComponents;

namespace Vant.UI.UIComponents.Editor
{
    [CustomEditor(typeof(CachedGridComponent))]
    public class CachedGridComponentEditor : UnityEditor.Editor
    {
        private CachedGridComponent _target;
        private SerializedProperty _templateProp;
        private SerializedProperty _containerProp;
        private SerializedProperty _poolSizeProp;
        private SerializedProperty _scrollDurationProp;
        private SerializedProperty _enableVirtualizationProp;
        private SerializedProperty _alignmentModeProp;
        private SerializedProperty _childAlignmentProp;
        private SerializedProperty _maxRowsProp;
        private SerializedProperty _maxColumnsProp;
        private SerializedProperty _cellSizeProp;
        private SerializedProperty _spacingProp;
        private SerializedProperty _enableScrollEffectProp;
        private SerializedProperty _scrollToTargetDurationProp;
        private SerializedProperty _scrollEaseTypeProp;

        private void OnEnable()
        {
            _target = (CachedGridComponent)target;
            _templateProp = serializedObject.FindProperty("itemTemplate");
            _containerProp = serializedObject.FindProperty("container");
            _poolSizeProp = serializedObject.FindProperty("poolSize");
            _enableScrollEffectProp = serializedObject.FindProperty("enableScrollEffect");
            _scrollToTargetDurationProp = serializedObject.FindProperty("scrollToTargetDuration");
            _scrollEaseTypeProp = serializedObject.FindProperty("scrollEaseType");
            _enableVirtualizationProp = serializedObject.FindProperty("enableVirtualization");
            _alignmentModeProp = serializedObject.FindProperty("_alignmentMode");
            _childAlignmentProp = serializedObject.FindProperty("_childAlignment");
            _maxRowsProp = serializedObject.FindProperty("_maxRows");
            _maxColumnsProp = serializedObject.FindProperty("_maxColumns");
            _cellSizeProp = serializedObject.FindProperty("_cellSize");
            _spacingProp = serializedObject.FindProperty("_spacing");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.PropertyField(_containerProp);

            EditorGUILayout.BeginHorizontal();
            
            EditorGUILayout.LabelField("Item Template", GUILayout.Width(EditorGUIUtility.labelWidth));

            float fieldWidth = 140f;
            if (_target.itemTemplate != null)
            {
                float textWidth = GUI.skin.label.CalcSize(new GUIContent(_target.itemTemplate.name)).x;
                fieldWidth = textWidth + 45f; 
            }

            EditorGUI.BeginChangeCheck();
            GameObject newTemplate = (GameObject)EditorGUILayout.ObjectField(
                _target.itemTemplate, 
                typeof(GameObject), 
                true, 
                GUILayout.Width(fieldWidth)
            );
            if (EditorGUI.EndChangeCheck())
            {
                _templateProp.objectReferenceValue = newTemplate;
                serializedObject.ApplyModifiedProperties();
            }

            if (_target.itemTemplate != null)
            {
                string skinName = null;
                string namespaceName = null;

                var generator = _target.itemTemplate.GetComponent<GridItemRendererGenerator>();
                var refGenerator = _target.itemTemplate.GetComponent<ReferenceContainerGenerator>();

                if (generator != null)
                {
                    skinName = generator.skinName;
                    namespaceName = generator.namespaceName;
                }
                else if (refGenerator != null)
                {
                    skinName = refGenerator.skinName;
                    namespaceName = refGenerator.namespaceName;
                }

                if (!string.IsNullOrEmpty(skinName))
                {
                    string fullTypeName = string.IsNullOrEmpty(namespaceName) ? skinName : $"{namespaceName}.{skinName}";
                    
                    // Auto-save the type name to the component
                    if (_target.skinTypeName != fullTypeName)
                    {
                        _target.skinTypeName = fullTypeName;
                        EditorUtility.SetDirty(_target);
                    }

                    GUILayout.Label($"Skin: {fullTypeName}", EditorStyles.miniLabel, GUILayout.ExpandWidth(false));
                }
                else
                {
                    GUIStyle yellowLabel = new GUIStyle(EditorStyles.miniLabel);
                    yellowLabel.normal.textColor = Color.yellow;
                    GUILayout.Label("Container component not found!", yellowLabel, GUILayout.ExpandWidth(false));
                }
            }
            
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.PropertyField(_poolSizeProp);
            
            EditorGUILayout.PropertyField(_enableScrollEffectProp, new GUIContent("Enable Scroll Effect"));
            if (_enableScrollEffectProp.boolValue)
            {
                EditorGUILayout.BeginVertical("box");
                EditorGUILayout.PropertyField(_scrollToTargetDurationProp, new GUIContent("Scroll To Target Duration"));
                EditorGUILayout.PropertyField(_scrollEaseTypeProp);
                EditorGUILayout.EndVertical();
            }

            EditorGUILayout.PropertyField(_enableVirtualizationProp);

            if (_enableVirtualizationProp.boolValue)
            {
                EditorGUILayout.BeginVertical("box");
                EditorGUILayout.PropertyField(_alignmentModeProp);
                if (_target.alignmentMode == GridAlignmentMode.Standard)
                {
                    EditorGUILayout.PropertyField(_childAlignmentProp);
                }
                EditorGUILayout.PropertyField(_maxColumnsProp);
                EditorGUILayout.PropertyField(_maxRowsProp);
                EditorGUILayout.PropertyField(_cellSizeProp);
                EditorGUILayout.PropertyField(_spacingProp);
                EditorGUILayout.EndVertical();
            }

            serializedObject.ApplyModifiedProperties();
        }
    }
}
