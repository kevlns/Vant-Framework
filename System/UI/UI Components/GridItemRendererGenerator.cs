using UnityEngine;
using System.Collections.Generic;
using System;

namespace Vant.UI.UIComponents
{
    public class GridItemRendererGenerator : MonoBehaviour
    {
        [Serializable]
        public class ReferenceData
        {
            public string alias;
            public UnityEngine.Object reference;
            public string typeName;
        }

        public string namespaceName;
        public string skinName;
        public string generatePath;
        public List<ReferenceData> references = new List<ReferenceData>();

        public UnityEngine.Object GetReference(string alias)
        {
            var item = references.Find(x => x.alias == alias);
            return item != null ? item.reference : null;
        }

        public T GetReference<T>(string alias) where T : class
        {
            var obj = GetReference(alias);
            if (obj == null) return null;

            if (obj is T val) return val;

            if (typeof(T) == typeof(GameObject))
            {
                if (obj is Component c) return c.gameObject as T;
            }
            
            if (typeof(Component).IsAssignableFrom(typeof(T)))
            {
                if (obj is GameObject go) return go.GetComponent<T>();
                if (obj is Component c) return c.GetComponent<T>();
            }

            return null;
        }
    }
}
