using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public interface IKillable
{
    // IsDead defines the character/object state and whether it can be killed or damaged
    bool IsDead { get; set; }


    // Kill will kill/destroy a character/object
    void Kill();

}
