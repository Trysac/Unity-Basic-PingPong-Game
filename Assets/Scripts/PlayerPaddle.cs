using RetroRally.Core;
using UnityEngine;

/// <summary>Player 1's paddle in the original scene, moved with the vertical axis in units per second.</summary>
public class PlayerPaddle : MonoBehaviour
{
    const float Limit = 3f;

    Rigidbody2D body;

    void Start()
    {
        body = GetComponent<Rigidbody2D>();
    }

    void Update()
    {
        body.linearVelocity = new Vector2(0f, Input.GetAxis("Vertical") * Tuning.PaddleSpeed);
        if (Mathf.Abs(transform.position.y) > Limit)
        {
            transform.position = new Vector2(transform.position.x, Mathf.Clamp(transform.position.y, -Limit, Limit));
        }
    }
}
