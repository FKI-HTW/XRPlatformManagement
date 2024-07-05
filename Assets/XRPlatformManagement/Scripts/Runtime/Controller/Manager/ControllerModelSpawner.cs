﻿using System;
using System.Collections.Generic;
using CENTIS.XRPlatformManagement.Controller.Elements;
using CENTIS.XRPlatformManagement.Controller.ProfileBuilding;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.XR;

namespace CENTIS.XRPlatformManagement.Controller.Manager
{
    /// <summary>
    /// Master class for all controller relevant implementations for this package. Will start instantiating/destroying corresponding controller model assets based on 'InputTracking'.
    /// </summary>
    public class ControllerModelSpawner : MonoBehaviour
    {
        #region Fields

        [SerializeField] private HandednessType _handednessType = HandednessType.Left;
        [Tooltip("The default Controller that will be shown, if no Controller was found. Can be Null, in which case no Controller will be instantiated!")]
        [SerializeField] private ControllerProfile _defaultProfile;
        [Tooltip("The default Controller will be active, event if a new device was enabled")]
        [SerializeField] private bool _alwaysShowDefault;
        [SerializeField] private Transform _modelParent;
        [SerializeField] private bool _worldPositionStaysOnInstantiate;
        [SerializeField] private bool _printManufacturerName;
        [Space]
        [SerializeField] private Events _events;
        
        public HandednessType Handedness => _handednessType;
        public bool HasTrackedDevice => _inputDeviceBufferProfile != null && !_alwaysShowDefault;

        private InputDevice _currentInputDevice;
        private readonly Dictionary<Enum, ControllerElementServiceLocator> _inputDeviceModelsLookup = new();
        private ControllerProfile _inputDeviceBufferProfile;
        private readonly Dictionary<Enum, ControllerElementServiceLocator> _defaultModelsLookup = new();
        private ControllerProfile _defaultBufferProfile;
        
        private static readonly Dictionary<string, ControllerProfile> Profiles = new();
        
        #endregion
        
        #region Unity Lifecycle

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void SetupAllProfiles()
        {
            // Lookup for existing supported Controllers and load them into a lookup-dict.
            foreach (ControllerProfile profile in Resources.LoadAll<ControllerProfile>("SupportedController"))
            {
                Profiles.TryAdd(profile.ManufacturerType, profile);
            }
        }
        
        private void Awake()
        {
            if (_modelParent == null)
            {
                _modelParent = new GameObject($"[{gameObject.name}] Model Parent").transform;
                _modelParent.SetParent(transform, false);
                _modelParent.localPosition = Vector3.zero;
                _modelParent.localRotation = Quaternion.identity;
            }
            
            if (_defaultProfile != null)
            {
                LoadController(ref _defaultBufferProfile, _defaultModelsLookup, _defaultProfile);
            }

            OnTrackingInitialized();
            _events.OnTrackingInitialized?.Invoke(this);
            InputTracking.trackingAcquired += OnTrackingAcquired;
            InputTracking.trackingLost += OnTrackingLost;
        }

        private void OnDestroy()
        {
            InputTracking.trackingAcquired -= OnTrackingAcquired;
            InputTracking.trackingLost -= OnTrackingLost;
            
            DestroyInputDevice(_defaultModelsLookup);
            _defaultBufferProfile = null;
            
            DestroyInputDevice(_inputDeviceModelsLookup);
            _inputDeviceBufferProfile = null;
        }

        private void OnEnable()
        {
            EnableInputDevice(HasTrackedDevice ? _inputDeviceModelsLookup : _defaultModelsLookup);
        }

        private void OnDisable()
        {
            DisableInputDevice(HasTrackedDevice ? _inputDeviceModelsLookup : _defaultModelsLookup);
        }

        #endregion
        
        #region Private Methods
        
        private void OnTrackingAcquired(XRNodeState node)
        {
            if (_alwaysShowDefault)
            {
                return;
            }
            
            InputDevice controller = InputDevices.GetDeviceAtXRNode(node.nodeType);
            
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (_printManufacturerName)
            {
                Debug.Log(controller.manufacturer);
            }
#endif
            
            if (!IsValidController(controller))
                return;

            if (Profiles.TryGetValue(controller.manufacturer, out var profile))
            {
                _currentInputDevice = controller;
                
                DisableInputDevice(_defaultModelsLookup);
                LoadController(ref _inputDeviceBufferProfile, _inputDeviceModelsLookup, profile);
                OnTrackingAcquired();
                
                _events.OnFinishTrackingAcquired?.Invoke(this);
            }
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            else
            {
                Debug.LogWarning($"Couldn't load profile with the name {controller.manufacturer} because it was not found at loading!");
            }
#endif
        }

        private void OnTrackingLost(XRNodeState node)
        {
            List<InputDevice> allDevices = new List<InputDevice>();
            InputDevices.GetDevices(allDevices);
            
            if (IsValidController(_currentInputDevice) && !allDevices.Contains(_currentInputDevice) && _inputDeviceBufferProfile != null)
            {
                _currentInputDevice = default;
                
                DestroyInputDevice(_inputDeviceModelsLookup);
                _inputDeviceBufferProfile = null;
                EnableInputDevice(_defaultModelsLookup);
                OnTrackingLost();

                _events.OnFinishTrackingLost?.Invoke(this);
            }
        }
        
        /// <summary>
        /// Check if its a controller and the correct side
        /// </summary>
        /// <param name="controller"></param>
        /// <returns></returns>
        private bool IsValidController(InputDevice controller)
        {
            return (controller.characteristics & InputDeviceCharacteristics.HeldInHand) != 0 &&
                   (controller.characteristics & InputDeviceCharacteristics.TrackedDevice) != 0 &&
                   (controller.characteristics & InputDeviceCharacteristics.Controller) != 0 &&
                   ((controller.characteristics & InputDeviceCharacteristics.Left) != 0) ==
                   (_handednessType == HandednessType.Left);
        }

        private void LoadController(ref ControllerProfile writtenProfile, Dictionary<Enum, ControllerElementServiceLocator> modelsLookup, ControllerProfile newProfile)
        {
            ControllerModel settings = _handednessType == HandednessType.Left ? newProfile.LeftHand : newProfile.RightHand;
            if (settings != null)
            {
                writtenProfile = newProfile;
                InitRenderModel(writtenProfile, modelsLookup, settings);
            }
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            else
            {
                Debug.LogError($"Controller couldn't be initialized!");
            }
#endif
        }

        private void DestroyInputDevice(Dictionary<Enum, ControllerElementServiceLocator> modelsLookup)
        {
            foreach (ControllerElementServiceLocator element in modelsLookup.Values)
            {
                Destroy(element.gameObject);
            }
            modelsLookup.Clear();
        }

        private void EnableInputDevice(Dictionary<Enum, ControllerElementServiceLocator> inputDeviceElements)
        {
            foreach (ControllerElementServiceLocator element in inputDeviceElements.Values)
            {
                element.gameObject.SetActive(true);
            }
        }

        private void DisableInputDevice(Dictionary<Enum, ControllerElementServiceLocator> inputDeviceElements)
        {
            foreach (ControllerElementServiceLocator element in inputDeviceElements.Values)
            {
                element.gameObject.SetActive(false);
            }
        }
        
        private void InitRenderModel(ControllerProfile bufferedProfile, Dictionary<Enum, ControllerElementServiceLocator> modelsLookup, ControllerModel controllerModel)
        {
            if (!bufferedProfile.UseParts)
            {
                InstantiateControllerElement(modelsLookup, ControllerModelMask.CompleteModel, controllerModel.GetModelByMask(ControllerModelMask.CompleteModel));
                return;
            }
            
            foreach (ControllerModelMask controllerButtonMask in (ControllerModelMask[]) Enum.GetValues(typeof(ControllerModelMask)))
            {
                if (controllerButtonMask != ControllerModelMask.CompleteModel && controllerButtonMask != ControllerModelMask.None)
                {
                    InstantiateControllerElement(modelsLookup, controllerButtonMask, controllerModel.GetModelByMask(controllerButtonMask));
                }
            }
        }

        private void InstantiateControllerElement(Dictionary<Enum, ControllerElementServiceLocator> modelsLookup, Enum key, GameObject element)
        {
            if (element == null) return;
            
            if (modelsLookup.ContainsKey(key))
            {
                Destroy(modelsLookup[key].gameObject);
                modelsLookup.Remove(key);
            }

            GameObject clone = Instantiate(element, _modelParent, _worldPositionStaysOnInstantiate);
            ControllerElementServiceLocator controllerElement = clone.AddComponent<ControllerElementServiceLocator>();
            controllerElement.Initialize(this, key);
            modelsLookup.Add(key, controllerElement);
        }
        
        #endregion
        
        #region Inheritance Methods
        
        protected virtual void OnTrackingInitialized() { }
        protected virtual void OnTrackingAcquired() { }
        protected virtual void OnTrackingLost() { }
        
        #endregion
        
        #region Public Methods

        public bool TryGetControllerProfileByManufacturerType(string manufacturerType, out ControllerProfile controllerProfile)
        {
            return Profiles.TryGetValue(manufacturerType, out controllerProfile);
        }

        public ControllerProfile GetCurrentControllerProfile()
        {
            return HasTrackedDevice ? _inputDeviceBufferProfile : _defaultBufferProfile;
        }
        
        public ControllerModel GetCurrentControllerModel()
        {
            ControllerProfile currentControllerProfile = GetCurrentControllerProfile();
            return GetControllerModelByProfile(currentControllerProfile);
        }
        
        /// <summary>
        /// Will return the 'ControllerModel' with the handedness of this component from a 'ControllerProfile'
        /// </summary>
        public ControllerModel GetControllerModelByProfile(ControllerProfile controllerProfile)
        {
            return _handednessType == HandednessType.Left
                ? controllerProfile.LeftHand
                : controllerProfile.RightHand;
        }
        
        public Dictionary<Enum, ControllerElementServiceLocator> GetCurrentModelsLookup()
        {
            return HasTrackedDevice ? _inputDeviceModelsLookup : _defaultModelsLookup;
        }
        
        public bool TryGetCurrentModelsLookupByType(ControllerModelMask controllerModelMask, out ControllerElementServiceLocator controllerHighlightable)
        {
            return GetCurrentModelsLookup().TryGetValue(controllerModelMask, out controllerHighlightable);
        }
        
        public bool TryGetCurrentModelsLookupByType(Enum controllerButtonMask, out ControllerElementServiceLocator controllerHighlightable)
        {
            return GetCurrentModelsLookup().TryGetValue(controllerButtonMask, out controllerHighlightable);
        }
        
        public void RegisterAction(EventType eventType, UnityAction<ControllerModelSpawner> action)
        {
            switch (eventType)
            {
                case EventType.OnTrackingInitialized:
                    _events.OnTrackingInitialized.AddListener(action.Invoke);
                    break;
                case EventType.OnFinishTrackingAcquired:
                    _events.OnFinishTrackingAcquired.AddListener(action.Invoke);
                    break;
                case EventType.OnFinishTrackingLost:
                    _events.OnFinishTrackingLost.AddListener(action.Invoke);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(eventType), eventType, null);
            }
        }
        
        public void UnregisterAction(EventType eventType, UnityAction<ControllerModelSpawner> action)
        {
            switch (eventType)
            {
                case EventType.OnTrackingInitialized:
                    _events.OnTrackingInitialized.RemoveListener(action.Invoke);
                    break;
                case EventType.OnFinishTrackingAcquired:
                    _events.OnFinishTrackingAcquired.RemoveListener(action.Invoke);
                    break;
                case EventType.OnFinishTrackingLost:
                    _events.OnFinishTrackingLost.RemoveListener(action.Invoke);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(eventType), eventType, null);
            }
        }
        
        #endregion
        
        #region Data Types
        
        public enum HandednessType
        {
            Left,
            Right
        }

        public enum EventType
        {
            OnTrackingInitialized,
            OnFinishTrackingAcquired,
            OnFinishTrackingLost
        }
        
        [Serializable]
        private class Events
        {
            [SerializeField] private UnityEvent<ControllerModelSpawner> _onTrackingInitialized;
            public UnityEvent<ControllerModelSpawner> OnTrackingInitialized => _onTrackingInitialized;
            
            [SerializeField] private UnityEvent<ControllerModelSpawner> _onFinishTrackingAcquired;
            public UnityEvent<ControllerModelSpawner> OnFinishTrackingAcquired => _onFinishTrackingAcquired;
            
            [SerializeField] private UnityEvent<ControllerModelSpawner> _onFinishTrackingLost;
            public UnityEvent<ControllerModelSpawner> OnFinishTrackingLost => _onFinishTrackingLost;
        }
        
        #endregion
    }
}