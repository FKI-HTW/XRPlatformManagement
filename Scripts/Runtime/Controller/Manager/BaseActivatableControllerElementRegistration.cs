﻿using System;
using UnityEngine;
using UnityEngine.Events;

namespace CENTIS.XRPlatformManagement.Controller.Manager
{
    /// <summary>
    /// Extends the ControllerElementRegistrator by an activation layer, that can be activated from another class or by inheritance.
    /// E.g. on button pressed down you can highlight it and when you release the button it will stop highlighting.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public abstract class BaseActivatableControllerElementRegistration<T> : BaseControllerElementRegistrator<T> where T : Component
    {
        #region Fields
        
        [Header("Activatable")]
        [Tooltip("Enable/Disable Tooltip within application")]
        [SerializeField] private bool _isEnabled = true;
        [SerializeField] private Events _events;
        
        
        public bool IsEnabled
        {
            get => _isEnabled;
            set
            {
                _isEnabled = value;

                if (!_isEnabled && IsCurrentlyActive)
                {
                    Deactivate();
                }
            }
        }

        protected bool IsCurrentlyActive;
        
        #endregion
        
        #region Public Methods

        /// <summary>
        /// Activates all elements managed by this class (e.g. tooltips, highlighting)
        /// </summary>
        public void Activate()
        {
            if (IsCurrentlyActive || !_isEnabled) return;
            IsCurrentlyActive = true;
            
            foreach (var keyValuePair in ElementLookup)
            {
                ActivateElement(keyValuePair.Value);
            }

            InternalOnActivateComplete();
            _events.OnActivate?.Invoke();
        }

        /// <summary>
        /// Deactivate all elements managed by this class (e.g. tooltips, highlighting)
        /// </summary>
        public void Deactivate()
        {
            if (!IsCurrentlyActive || !_isEnabled) return;
            IsCurrentlyActive = false;
            
            foreach (var keyValuePair in ElementLookup)
            {
                DeactivateElement(keyValuePair.Value);
            }

            InternalOnDeactivateComplete();
            _events.OnDeactivate?.Invoke();
        }
        
        public void RegisterAction(EventType eventType, UnityAction action)
        {
            switch (eventType)
            {
                case EventType.OnActivate:
                    _events.OnActivate.AddListener(action.Invoke);
                    break;
                case EventType.OnDeactivate:
                    _events.OnDeactivate.AddListener(action.Invoke);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(eventType), eventType, null);
            }
        }
        
        public void UnregisterAction(EventType eventType, UnityAction action)
        {
            switch (eventType)
            {
                case EventType.OnActivate:
                    _events.OnActivate.RemoveListener(action.Invoke);
                    break;
                case EventType.OnDeactivate:
                    _events.OnDeactivate.RemoveListener(action.Invoke);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(eventType), eventType, null);
            }
        }
        
        #endregion
        
        #region Inheritance Methods

        protected abstract override void InitializeElement(Enum buttonType, T element);
        protected abstract void ActivateElement(T element);
        protected abstract void DeactivateElement(T element);
        protected virtual void InternalOnActivateComplete() {}
        protected virtual void InternalOnDeactivateComplete() {}
        
        #endregion
        
        #region Data Types
        
        public enum EventType { OnActivate, OnDeactivate }
        
        [Serializable]
        private class Events
        {
            [SerializeField] private UnityEvent _onActivate;
            public UnityEvent OnActivate => _onActivate;
            
            [SerializeField] private UnityEvent _onDeactivate;
            public UnityEvent OnDeactivate => _onDeactivate;
        }
        
        #endregion
    }
}
