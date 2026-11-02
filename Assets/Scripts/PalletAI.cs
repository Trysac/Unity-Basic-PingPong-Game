using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PalletAI : MonoBehaviour
{
    [SerializeField] float moveSpeed = 1f;

    void Update()
    {
        if (FindFirstObjectByType<Ball>()) 
        {
            transform.position = new Vector2(transform.position.x, Mathf.Lerp(transform.position.y, FindFirstObjectByType<Ball>().transform.position.y, moveSpeed * Time.deltaTime));
        }      
        
        if (transform.position.y < -3 || transform.position.y > 3)
        {
            float clampPosition = Mathf.Clamp(transform.position.y, -3, 3);
            transform.position = new Vector2(transform.position.x, clampPosition);
        }
    }
}
