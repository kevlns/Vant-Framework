using System;
using System.Collections.Generic;

namespace Vant.MVC
{
    /// <summary>
    /// A wrapper class for data binding.
    /// </summary>
    /// <typeparam name="T">The type of the value.</typeparam>
    public class BindableProperty<T>
    {
        private T _oldValue;
        
        // Action to notify the listener when value changes
        private Action<T> _onValueChanged;

        public T Value
        {
            get => _oldValue;
            set
            {
                // Check for equality
                if (!EqualityComparer<T>.Default.Equals(_oldValue, value))
                {
                    _oldValue = value;
                    // Activate update
                    _onValueChanged?.Invoke(_oldValue);
                }
            }
        }

        public BindableProperty(T initialValue = default)
        {
            _oldValue = initialValue;
        }

        /// <summary>
        /// Binds a callback to this property.
        /// </summary>
        /// <param name="callback">The action to execute when value changes.</param>
        /// <param name="triggerImmediately">If true, the callback is invoked immediately with the current value.</param>
        public void Bind(Action<T> callback, bool triggerImmediately = true)
        {
            _onValueChanged += callback;
            if (triggerImmediately)
            {
                callback?.Invoke(_oldValue);
            }
        }

        /// <summary>
        /// Unbinds a callback.
        /// </summary>
        public void Unbind(Action<T> callback)
        {
            _onValueChanged -= callback;
        }

        /// <summary>
        /// Clears all bindings.
        /// </summary>
        public void UnbindAll()
        {
            _onValueChanged = null;
        }
    }
}
