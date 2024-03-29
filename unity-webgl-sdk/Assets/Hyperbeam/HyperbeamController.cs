﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Events;

namespace Hyperbeam
{
    // Subclassing this class will result in undefined behaviour. Everything works properly when it's not subclass
    // so please don't
    public sealed class HyperbeamController : MonoBehaviour 
    {
        /// <summary>
        /// The accessor for the underlying Hyperbeam object.
        /// </summary>
        public Hyperbeam Instance;

        /// <summary>
        /// OnHyperbeamStart will notify any registered events when this controller's hyperbeam instance is accessible.
        /// </summary>
        public UnityEvent OnHyperbeamStart;

        /// <summary>
        /// OnTextureReady is the callback used by <see cref="HyperbeamVideoSource"/> to know when a texture is ready to be applied.
        /// </summary>
        public Action<Texture2D> OnTextureReady;

        /// <summary>
        /// OnControlReturned will notify any registered events when control is returned to unity after being handed off by <see cref="PassControlToBrowser"/>
        /// </summary>
        public UnityEvent OnControlReturned;

        /// <summary>
        /// OnHyperbeamStop is fired when a call is made to <see cref="StopHyperbeamStream"/>
        /// It will let any listeners know that the instance is about to be disposed.
        /// </summary>
        public UnityEvent OnHyperbeamStopped;

        private bool _hyperbeamControl = false;
        private float _volume = 0f;
        
        /// <summary>
        /// Volume of the hyperbeam instance in the range [0, 1]
        /// </summary>
        public float Volume
        {
            get
            {
                return _volume;
            }
            set
            {
                _volume = value;
                Instance.Volume = value;
            }
        }

        private bool _isPaused = false;
        
        /// <summary>
        /// Setting this to true will pause the video stream, but the audio stream will remain playing.
        /// </summary>
        public bool Paused
        {
            get
            {
                return _isPaused;
            }
            set
            {
                _isPaused = value;
                Instance.SetVideoPause(value);
            }
        }

        /// <summary>
        ///     <para>
        ///         The entrypoint for the hyperbeam instance to be started. This will interface with the web browser to inject a hyperbeam stream into unity.
        ///         This function is required to be called first for any other functions to have an effect.
        ///     </para>
        ///     <para>
        ///         When the HyperbeamController GameObject is disabled, the stream is simply paused and muted. This reduces bandwidth drastically, but keeps resources allocated.
        ///         If a disabled stream is re-enabled, the previous settings will be restored.
        ///     </para>
        ///     <para>
        ///         When a game object is fully disposed, either automatically, or by calling Dispose, the stream will be fully torn down and all resources will be released.
        ///     </para>
        /// </summary>
        /// <param name="embedUrl">A URL provided by the hyperbeam API. See <a href="https://docs.hyperbeam.com/home/getting-started">here</a> for information on how to generate one.</param>
        public void StartHyperbeamStream(string embedUrl)
        {
            // Debug.Log("Created new hyperbeam instance...");
            Instance = new Hyperbeam(embedUrl, gameObject);
        }

        /// <summary>
        /// Stops the stream and disposes the Instance to make the controller safe to re-use.
        /// Will invoke the <see cref="OnHyperbeamStopped"/> event to notify any listeners that the instance will be disposed soon.
        /// </summary>
        public void StopHyperbeamStream()
        {
            OnHyperbeamStopped?.Invoke();
            Instance?.Dispose();
            Instance = null;
        }

        private void Start()
        {
            OnControlReturned ??= new UnityEvent();
            OnHyperbeamStart ??= new UnityEvent();
            OnHyperbeamStopped ??= new UnityEvent();
        }

        private void OnDestroy()
        {
            StopHyperbeamStream();
        }

        private void OnDisable()
        {
            if (Instance == null) return;
            Instance.Volume = 0f;
            Instance.SetVideoPause(true);
        }

        private void OnEnable()
        {
            if (Instance == null) return;
            Instance.Volume = Volume;
            Instance.SetVideoPause(Paused);
        }

        /// <summary>
        /// Called by the hyperbeam JSLIB to communicate with unity. Please register an event handler to <see cref="OnHyperbeamStart"/> 
        /// to get notified when hyperbeam has started.
        /// </summary>
        public void HyperbeamCallback(int id)
        {
            Instance.InstanceId = id;
            // Debug.Log($"Bound new instance to id: {id}");
            // Debug.Log("Waiting for new texture...");
            StartCoroutine(Instance.GetHyperbeamTexture(OnTextureReady));
            
            _volume = Instance.Volume;
            OnHyperbeamStart?.Invoke();
        }

        /// <summary>
        ///     <para>
        ///         Will pass control to Hyperbeam's JsLib which will install event listeners for keyboard events
        ///         During this time hyperbeam will keep focus until it receives a keydown from closeKey with the correct modifier keys.
        ///     </para>
        ///     <para>
        ///         Register an event handler to <see cref="OnControlReturned"/> if you would like to recieve a notification when the use has "finished" interacting with Hyperbeam.
        ///     </para>
        /// </summary>
        /// <param name="closeKey">The Keydown that will trigger unity regaining control</param>
        /// <param name="ctrl">Whether or not the ctrl key must be held down to regain control</param>
        /// <param name="meta">Whether or not the meta key must be held down to regain control</param>
        /// <param name="alt">Whether or not the alt key must be held down to regain control</param>
        /// <param name="shift">Whether or not the shift key must be held down to regain control</param>
        public void PassControlToBrowser(string closeKey, bool ctrl, bool meta, bool alt, bool shift)
        {
            if (Instance == null) return;
            
#if !UNITY_EDITOR && UNITY_WEBGL
            WebGLInput.captureAllKeyboardInput = false;
#endif
            if (_hyperbeamControl) return;
            Instance.GiveHyperbeamControl(closeKey, ctrl, meta, alt, shift);
            _hyperbeamControl = true;
        }

        /// <summary>
        /// Called by the hyperbeam JSLIB to communicate with unity. Please register an event handler to <see cref="OnControlReturned"/> 
        /// to get notified when the user has "finished" interacting with hyperbeam.
        /// </summary>
        public void ReceiveControlFromBrowser()
        {
#if !UNITY_EDITOR && UNITY_WEBGL
            WebGLInput.captureAllKeyboardInput = true;
#endif
            _hyperbeamControl = false;
            OnControlReturned?.Invoke();
        }

        /// <summary>
        /// This function will take control back from hyperbeam on the unity side and unregister the keyup and down handler associated with hyperbeam.
        /// </summary>
        public void TakeBackControlFromBrowser()
        {
            ReceiveControlFromBrowser();
            Instance.TakeBackControl();
        }
    }
}
