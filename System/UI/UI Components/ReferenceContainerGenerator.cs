using UnityEngine;
using System.Collections.Generic;
using System;

namespace Vant.UI.UIComponents
{
    public class ReferenceContainerGenerator : MonoBehaviour
    {
        public enum ReferenceType
        {
            Object,
            ObjectArray,
            Int,
            Int2,
            Int3,
            Float,
            Float2,
            Float3,
            String,
            Bool
        }

        [Serializable]
        public class ReferenceData
        {
            public string alias;
            public ReferenceType type;
            public string typeName;

            [UnityEngine.Serialization.FormerlySerializedAs("reference")]
            public UnityEngine.Object objectValue;
            public UnityEngine.Object[] objectArrayValue;

            public int intValue;
            public Vector2Int int2Value;
            public Vector3Int int3Value;

            public float floatValue;
            public Vector2 float2Value;
            public Vector3 float3Value;

            public string stringValue;
            public bool boolValue;
        }

        public string namespaceName;
        public string skinName;
        public string generatePath;
        public List<ReferenceData> references = new List<ReferenceData>();

        public UnityEngine.Object GetReference(string alias)
        {
            var item = references.Find(x => x.alias == alias);
            return item != null && item.type == ReferenceType.Object ? item.objectValue : null;
        }

        public T Get<T>(string alias)
        {
            var item = references.Find(x => x.alias == alias);
            if (item == null) return default;

            switch (item.type)
            {
                case ReferenceType.Object:
                    return (T)ConvertObject(item.objectValue, typeof(T));

                case ReferenceType.ObjectArray:
                    if (item.objectArrayValue is T directArr) return directArr;
                    if (typeof(T).IsArray)
                    {
                        var elementType = typeof(T).GetElementType();
                        if (typeof(UnityEngine.Object).IsAssignableFrom(elementType))
                        {
                            var sourceArr = item.objectArrayValue;
                            if (sourceArr == null) return default;
                            var newArr = Array.CreateInstance(elementType, sourceArr.Length);
                            for (int i = 0; i < sourceArr.Length; i++)
                            {
                                newArr.SetValue(ConvertObject(sourceArr[i], elementType), i);
                            }
                            return (T)(object)newArr;
                        }
                    }
                    break;

                case ReferenceType.Int:
                    if (typeof(T) == typeof(int)) return (T)(object)item.intValue;
                    break;
                case ReferenceType.Int2:
                    if (typeof(T) == typeof(Vector2Int)) return (T)(object)item.int2Value;
                    break;
                case ReferenceType.Int3:
                    if (typeof(T) == typeof(Vector3Int)) return (T)(object)item.int3Value;
                    break;
                case ReferenceType.Float:
                    if (typeof(T) == typeof(float)) return (T)(object)item.floatValue;
                    break;
                case ReferenceType.Float2:
                    if (typeof(T) == typeof(Vector2)) return (T)(object)item.float2Value;
                    break;
                case ReferenceType.Float3:
                    if (typeof(T) == typeof(Vector3)) return (T)(object)item.float3Value;
                    break;
                case ReferenceType.String:
                    if (typeof(T) == typeof(string)) return (T)(object)item.stringValue;
                    break;
                case ReferenceType.Bool:
                    if (typeof(T) == typeof(bool)) return (T)(object)item.boolValue;
                    break;
            }

            return default;
        }

        public T GetReference<T>(string alias) where T : class
        {
            return Get<T>(alias);
        }

        private object ConvertObject(UnityEngine.Object obj, Type targetType)
        {
            if (obj == null) return null;
            if (targetType.IsInstanceOfType(obj)) return obj;

            if (targetType == typeof(GameObject))
            {
                if (obj is Component c) return c.gameObject;
            }

            if (typeof(Component).IsAssignableFrom(targetType))
            {
                if (obj is GameObject go) return go.GetComponent(targetType);
                if (obj is Component c) return c.GetComponent(targetType);
            }

            return null;
        }
    }
}