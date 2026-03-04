using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ItemDrop : MonoBehaviour
{
    float gravity = -0.5f;
    [SerializeField] float minhorzForce = -2;
    [SerializeField] float maxhorzForce = 2;
    [SerializeField] float minVertForce = 5f;
    [SerializeField] float maxVertForce = 5f;
    [SerializeField] float timeToActivateCollider = 0.25f;

    Controller2D controller2D;
    AudioSource audioSource;
    Vector2 moveAmount;
    //PolygonCollider2D polygonCollider2D;  // remove polygonCollider2D from Item gameObjects

    void Start()
    {
        controller2D = GetComponent<Controller2D>();
        audioSource = GetComponent<AudioSource>();
        //polygonCollider2D = GetComponent<PolygonCollider2D>();

        // play sound of item on start
        if (audioSource != null) { audioSource.Play(); }

        DropItem();
    }
    void FixedUpdate()
    {
        Move();
    }

    public void Move()
    {
        moveAmount.y += gravity * Time.deltaTime;
        controller2D.Move(moveAmount, Vector2.zero, false, false);
        if (controller2D.collisions.above || controller2D.collisions.below)
        {
            moveAmount.y = 0;
            moveAmount.x = 0;
        }
    }
    public void DropItem()
    {
        float xForce = UnityEngine.Random.Range(minhorzForce, maxhorzForce);
        xForce = Mathf.Round(xForce * 10) / 10;

        float yForce = UnityEngine.Random.Range(minVertForce, maxVertForce);
        yForce = Mathf.Round(yForce * 10) / 10;

        Vector2 dropForce = new Vector2(xForce, yForce);
        moveAmount += dropForce * Time.deltaTime;

        StartCoroutine(DestroyPickup());
    }

    IEnumerator DestroyPickup()
    {
        yield return new WaitForSeconds(timeToActivateCollider);
        gameObject.SetActive(false);
    }
}
