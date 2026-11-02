using RetroRally.Core;
using UnityEngine;

/// <summary>
/// Player 2's paddle in the original scene, steered by the engine-free CPU at a capped speed
/// instead of snapping towards the ball.
/// </summary>
public class CpuPaddle : MonoBehaviour
{
    const float Limit = 3f;

    [SerializeField] Difficulty difficulty = Difficulty.Normal;

    CpuPlayer cpu;
    Paddle model;
    Rigidbody2D ballBody;
    readonly RetroRally.Core.Ball[] balls = { new RetroRally.Core.Ball() };

    void Start()
    {
        cpu = new CpuPlayer(Side.Right, difficulty, new Rng(Random.Range(int.MinValue, int.MaxValue)));
        model = new Paddle(Side.Right, GetComponent<Collider2D>().bounds.extents.y);
        Ball ball = FindFirstObjectByType<Ball>();
        ballBody = ball != null ? ball.GetComponent<Rigidbody2D>() : null;
    }

    void Update()
    {
        if (ballBody == null)
        {
            return;
        }
        // Describe the scene in the court's coordinates (the CPU expects its paddle at +PaddleX).
        Vector2 offset = new Vector2(Tuning.PaddleX - transform.position.x, 0f);
        balls[0].Position = ToVec(ballBody.position + offset);
        balls[0].Velocity = ToVec(ballBody.linearVelocity);
        model.Place(transform.position.y);
        float input = cpu.Think(model, balls, Time.deltaTime);
        float y = Mathf.Clamp(transform.position.y + input * Tuning.PaddleSpeed * Time.deltaTime, -Limit, Limit);
        transform.position = new Vector2(transform.position.x, y);
    }

    static Vec2 ToVec(Vector2 v) => new Vec2(v.x, v.y);
}
