using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class VirtualPlayerControler : MonoBehaviour
{

    //find player avatar
    [SerializeField] private Transform avatarplayer;


    //selfcollision detection
    [SerializeField] private LayerMask ghostlayer;


    //movement
    public float horizontal;
    public float vertical;
    public float speed;
    [SerializeField] private Rigidbody2D rb;



    public void Update()
    {
        horizontal = Input.GetAxisRaw("Horizontal");
        vertical = Input.GetAxisRaw("Vertical");
        Physics2D.IgnoreLayerCollision(6, 8);
        moveplayer();
        //6 is the visible walls

    }

    public void moveplayer()
    {

        rb.velocity = new Vector2(horizontal * speed, vertical * speed);
        //transform.position += new Vector3(horizontal* speed * Time.deltaTime, vertical * speed * Time.deltaTime, 0);
    }

}
