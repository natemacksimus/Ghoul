using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Damage : MonoBehaviour
{
    [SerializeField] CharacterStats characterStats;

    [SerializeField] bool hasMultiTargetEffect = false;

    [SerializeField] string[] tagsDamageable;

    [SerializeField] List<GameObject> gameObjectsHit;

    [SerializeField] Collider2D hitObjectCollider;

    [SerializeField] private int targetsHit;

    [SerializeField] private bool disableDamageAfterHit = false;
    [SerializeField] private bool damageDisabled = false;

    private AudioSource audioSource;

    [SerializeField] private float extraKnockback = 0f;
    [SerializeField] private float extraKnockbackTime = 0f;

    [Header("Damager's Info")]
    [SerializeField] private int faceDir;
    [SerializeField] private float damageToInflict;
    [SerializeField] private Vector2 knockbackPower;
    [SerializeField] private float knockbackTime;
    [SerializeField] private AudioClip hitSound;
    [SerializeField] private GameObject target;


    private void Start()
    {
        characterStats = GetComponentInParent<CharacterStats>();
        audioSource = GetComponentInParent<AudioSource>();
        hitObjectCollider = GetComponent<Collider2D>();

        // if no collider is attached to this Damage script's game object, find it in parent
        //if (hitObjectCollider == null) { GetComponentInParent<Collider2D>(); }
    }

    private void GetDamagersInfo()
    {
        if (characterStats != null)
        {
            faceDir = characterStats.GetComponent<GameCharacterController>().IsFacingRight ? 1 : -1;
            damageToInflict = characterStats.GetCurrentDamage();
            knockbackPower = characterStats.GetKnockbackPower();
            knockbackTime = characterStats.GetKnockbackTime();
            hitSound = characterStats.WeaponHitSound;
            target = characterStats.gameObject;
        }
        else
        {
            faceDir = (int)gameObject.transform.localScale.x;
            target = GameObject.FindGameObjectWithTag("Player");
            //Debug.Log("faceDir: " + faceDir);
            //damageToInflict = 0;
            //knockbackPower = Vector2.zero;
            //knockbackTime = 0;
            //hitSound = null;
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (damageDisabled == true) { Debug.Log("Damage disabled"); return; }
        
        if (other.tag == "Untagged")
        {
            //Debug.Log("Ignore Untagged");
            return;
        }

        if (gameObjectsHit.Contains(other.gameObject))
        {
            Debug.Log("multiple hits on 1 target.");
            return;
        }
        else
        {
            //Debug.Log("gameObjectsHit now: " + other.gameObject);
            //if (gameObjectsHit.Count > 0) { Debug.Log("gameObjectsHit prev: " + gameObjectsHit[0]); }
            if (gameObjectsHit.Count > 0) { Debug.Log("now: " + other.gameObject + "; prev: " + gameObjectsHit[0]); }

        }

        if (tagsDamageable == null)
        {
            return;
        }

        for (int i = 0; i < tagsDamageable.Length; i++)
        {
            if (other.tag == tagsDamageable[i])
            {
                gameObjectsHit.Add(other.gameObject);

                if (!hasMultiTargetEffect && gameObjectsHit.Count > 1) { return; }

                // hit npc and damage or downed him.
                IDamageable characterHit = other.GetComponent<IDamageable>();

                //Debug.Log("about to inflict damage " + characterHit);
                //Debug.Log("about to inflict damage: " + characterStats.GetCurrentDamage());

                if (characterHit == null) { return; }

                GetDamagersInfo();

                //Debug.Log("damageToInflict: " + damageToInflict + "; knockbackPower: " + knockbackPower);
                //Debug.Log("knockbackTime: " + knockbackTime + "; faceDir: " + faceDir);
                //Debug.Log("characterHit: " + other.gameObject);

                // Inflict damage on the character hit.
                characterHit.InflictDamage(damageToInflict);

                // Apply extra knockback if the hit causes death
                CharacterStats dmgReceiverStats = other.gameObject.GetComponent<CharacterStats>();
                bool isDeathBlow = false;
                
                if (dmgReceiverStats != null) { if (dmgReceiverStats.GetCurrentHealth() <= 0) { isDeathBlow = true; } }

                if (isDeathBlow) 
                {
                    knockbackPower = new Vector2(knockbackPower.x + extraKnockback, knockbackPower.y);
                    knockbackTime += extraKnockbackTime;
                }

                // Inflict knockback on the character hit
                characterHit.Knockback(knockbackPower, knockbackTime, faceDir);

                // Play Hit sound
                if (hitSound != null)
                {
                    if (audioSource == null) { return; }
                    audioSource.PlayOneShot(hitSound);
                }

                // Set current target of hit creature to the player
                //CreatureController creatureController = other.gameObject.GetComponent<CreatureController>();
                //if (creatureController != null && target != null) { creatureController.CurrentTarget = target; }
                
                
                targetsHit++;

                //Debug.Log("inflicted damage");

                if (disableDamageAfterHit == true) { damageDisabled = true; }
            }
        }
    }

    public void DisableDamage(bool isDisabled)
    {
        damageDisabled = isDisabled;
    }

    public void EnableHitObject()
    {
        if (hitObjectCollider == null) { return; }
        targetsHit = 0;
        hitObjectCollider.enabled = true;
    }
    public void DisableHitObject()
    {
        if (hitObjectCollider == null) { return; }
        hitObjectCollider.enabled = false;
        ClearHitList();
    }
    public void ClearHitList()
    {
        gameObjectsHit.Clear();
    }
}
