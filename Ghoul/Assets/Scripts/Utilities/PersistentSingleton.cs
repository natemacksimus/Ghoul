using System.Collections;
using System.Collections.Generic;
using UnityEngine;



// Any script that needs persistent singleton needs to inherit from this.  Attaching this to a gameObject is not an option.
public class PersistentSingleton<T> : MonoBehaviour where T : Component
{
    //Only works on GameObjects at the root level
    public bool AutoUnparentOnAwake = true;

    protected static T instance;

    public static bool HasInstance => instance != null;
    public static T TryGetInstance() => HasInstance ? instance : null;

    public static T Instance
    {
        get
        {
            if (instance == null)
            {
                instance = FindObjectOfType<T>();
                if (instance == null)
                {
                    Debug.LogWarning($"No instance of {typeof(T).Name} found in the scene. Add a new one to scene.");

                    // create a new GameManager if one does not exist
                    var go = new GameObject(name: typeof(T).Name + " Auto-Generated");
                    instance = go.AddComponent<T>();

                    // Prevent this new GameObject from being saved in the scene
                    go.hideFlags = HideFlags.HideAndDontSave;
                }
            }

            return instance;
        }
    }

    // Make sure to call base.Awake() in override if you need awake
    protected virtual void Awake()
    {
        InitializeSingleton();
    }

    protected virtual void InitializeSingleton()
    {
        if (!Application.isPlaying) { return; }

        if (AutoUnparentOnAwake)
        {
            transform.SetParent(p: null);
        }

        if (instance == null)
        {
            instance = this as T;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            if (instance != this)
            {
                Destroy(gameObject);
            }
        }
    }

    // Not needed because when get is called on instance, it will create one if none exists
    //private void OnDestroy()
    //{
    //    if (instance == this)
    //    {
    //        instance = null;
    //    }
    //}
}
