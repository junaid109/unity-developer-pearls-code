using System.Collections.Generic;
using UnityEngine;

namespace Pearls.Enterprise
{
    [SelectionBase]
    public class BIMDataContainer : MonoBehaviour
    {
        // Use a serializable dictionary or two lists for the Inspector
        [SerializeField] private List<string> keys = new List<string>();
        [SerializeField] private List<string> values = new List<string>();

        public void AddData(string key, string value)
        {
            keys.Add(key);
            values.Add(value);
        }

        public string GetValue(string key)
        {
            int index = keys.IndexOf(key);
            return index != -1 ? values[index] : null;
        }
    }
}
