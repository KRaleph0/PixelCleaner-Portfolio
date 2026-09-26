using UnityEngine;
using UnityEngine.XR.ARFoundation;

namespace PixelCleaners.AR
{
    [RequireComponent(typeof(ARSession))]
    public class ARSessionManager : MonoBehaviour
    {
        public static ARSessionManager Instance { get; private set; }

        public bool IsSessionReady { get; private set; }

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        void OnEnable()  => ARSession.stateChanged += OnStateChanged;
        void OnDisable() => ARSession.stateChanged -= OnStateChanged;

        void OnStateChanged(ARSessionStateChangedEventArgs args)
        {
            IsSessionReady = args.state == ARSessionState.SessionTracking;
            Debug.Log($"[ARSession] State: {args.state}");
        }
    }
}
