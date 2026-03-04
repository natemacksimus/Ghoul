using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

public class EventSys : PersistentSingleton<EventSys>
{
    // Inherits from PersistentSingleton<EventSys>


    //private static EventSys instance;

    //public static EventSys Instance { get => instance; }

    protected override void Awake()
    {
        base.Awake();

        EventSystem instanceEventSystem = instance.GetComponent<EventSystem>();

        if (instanceEventSystem != null)
        {
            if (instanceEventSystem.enabled == false)
            {
                //Debug.Log("instance is this");
                instanceEventSystem.enabled = true;
            }
        }
    }

    //private void Awake()
    //{
    //    transform.SetParent(null);
    //    //DontDestroyOnLoad(gameObject);

    //    if (instance == null)
    //    {
    //        instance = this;
    //        DontDestroyOnLoad(gameObject);
    //    }
    //    else
    //    {
    //        if (instance != this)
    //        {
    //            Destroy(gameObject);
    //        }
    //    }
    //}

    //private void OnDestroy()
    //{
    //    if (instance == this)
    //    {
    //        instance = null;
    //    }
    //}

}
