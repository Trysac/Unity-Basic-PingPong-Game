using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Pallet : MonoBehaviour
{
    [SerializeField] float moveSpeed = 2000f;
    
    Rigidbody2D myRigidbody2D;

    void Start()
    {
        myRigidbody2D = GetComponent<Rigidbody2D>();
    }


    void Update()
    {
        myRigidbody2D.velocity = new Vector2(0 , Input.GetAxis("Vertical") * moveSpeed * Time.deltaTime);
        if (transform.position.y < -3 ||transform.position.y > 3) 
        {
            float clampPosition = Mathf.Clamp(transform.position.y, -3, 3);
            transform.position = new Vector2(transform.position.x, clampPosition);
        }
    }
}
