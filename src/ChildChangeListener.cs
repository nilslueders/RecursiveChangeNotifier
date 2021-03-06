namespace ThomasJaworski.ComponentModel
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Specialized;
    using System.ComponentModel;
    using System.Diagnostics;
    using System.Linq;
    using System.Reflection;

    public class ChildChangeListener : ChangeListener
    {
        #region *** Members ***
        private readonly INotifyPropertyChanged value;
        private readonly Type type;
        private readonly Dictionary<string, ChangeListener> childListeners = new Dictionary<string, ChangeListener>();
        #endregion


        #region *** Constructors ***
        public ChildChangeListener(INotifyPropertyChanged instance)
        {
            if (instance == null)
                throw new ArgumentNullException(nameof(instance));

            Debug.WriteLine($"creating {nameof(ChildChangeListener)} for {instance}");

            value = instance;
            type = value.GetType();

            Subscribe();
        }

        public ChildChangeListener(INotifyPropertyChanged instance, string propertyName)
            : this(instance)
        {
            PropertyName = propertyName;
        }
        #endregion


        #region *** Private Methods ***
        private void Subscribe()
        {
            value.PropertyChanged += value_PropertyChanged;

            foreach (var property in type.GetTypeInfo().DeclaredProperties)
            {
                if (!IsPubliclyReadable(property))
                    continue;
                if (!IsNotifier(property.GetValue(obj: this.value)))
                    continue;

                ResetChildListener(property.Name);
            }
        }

        private static bool IsPubliclyReadable(PropertyInfo prop) => (prop.GetMethod?.IsPublic ?? false) && !prop.GetMethod.IsStatic;
        static bool IsNotifier(object value) => (value is INotifyCollectionChanged) || (value is INotifyPropertyChanged);

        /// <summary>
        /// Resets known (must exist in children collection) child event handlers
        /// </summary>
        /// <param name="propertyName">Name of known child property</param>
        private void ResetChildListener(string propertyName)
        {
            ChangeListener listener;
            // Unsubscribe if existing
            if (childListeners.TryGetValue(propertyName, out listener)
                && listener != null)
            {
                listener.PropertyChanged -= child_PropertyChanged;

                // Should unsubscribe all events
                listener.Dispose();
                listener = null;
                childListeners.Remove(propertyName);
            }

            PropertyInfo property = this.type.GetProperty(propertyName);
            if (property == null)
                throw new InvalidOperationException(
                    $"Was unable to get '{propertyName}' property information from Type '{type.Name}'");

            object newValue = property.GetValue(value, null);

            // Only recreate if there is a new value
            if (newValue != null)
            {
                if (newValue is INotifyCollectionChanged)
                {
                    listener = childListeners[propertyName] =
                        new CollectionChangeListener(newValue as INotifyCollectionChanged, propertyName);
                }
                else if (newValue is INotifyPropertyChanged)
                {
                    listener = childListeners[propertyName] =
                        new ChildChangeListener(newValue as INotifyPropertyChanged, propertyName);
                }
                else
                {
                    Debug.WriteLine($"not listening to {propertyName} as {newValue} is {newValue.GetType()}");
                }

                if (listener != null)
                    listener.PropertyChanged += child_PropertyChanged;
            }
        }
        #endregion


        #region *** Event Handler ***
        void child_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            RaisePropertyChanged(propertyName: e.PropertyName);
        }

        void value_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            // First, reset child on change, if required...
            ResetChildListener(e.PropertyName);

            // ...then, notify about it
            RaisePropertyChanged(propertyName: e.PropertyName);
        }

        protected override void RaisePropertyChanged(string propertyName)
        {
            // Special Formatting
            base.RaisePropertyChanged($"{PropertyName}{(PropertyName != null ? "." : null)}{propertyName}");
        }
        #endregion


        #region *** Overrides ***
        /// <summary>
        /// Release all child handlers and self handler
        /// </summary>
        protected override void Unsubscribe()
        {
            value.PropertyChanged -= value_PropertyChanged;

            foreach (var binderKey in childListeners.Keys)
            {
                if (childListeners[binderKey] != null)
                    childListeners[binderKey].Dispose();
            }

            childListeners.Clear();

            Debug.WriteLine("ChildChangeListener '{0}' unsubscribed", PropertyName);
        }
        #endregion
    }
}
