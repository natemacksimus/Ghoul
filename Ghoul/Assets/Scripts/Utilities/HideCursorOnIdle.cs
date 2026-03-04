using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class HideCursorOnIdle : MonoBehaviour
{
    [SerializeField] private bool enableHideMouse = true;
    [SerializeField] private float idleTimeThreshold = 1.0f; // Adjust this threshold as needed
    [ShowOnly][SerializeField] private float lastMouseMoveTime;

    void Start()
    {
        Cursor.visible = true; // Initially, make sure the cursor is visible
        lastMouseMoveTime = Time.time; // Record the initial time
    }

    void Update()
    {
        if (!enableHideMouse) { return; }
        
        if (Input.GetAxis("Mouse X") != 0 || Input.GetAxis("Mouse Y") != 0)
        {
            // Mouse moved, update last move time and make cursor visible
            Cursor.visible = true;
            lastMouseMoveTime = Time.time;
        }
        else
        {
            // Mouse not moved, check idle time to hide cursor
            if (Time.time - lastMouseMoveTime > idleTimeThreshold)
            {
                Cursor.visible = false;
            }
        }
    }
}
