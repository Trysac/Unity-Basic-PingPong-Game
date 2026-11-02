using System.Collections;
using RetroRally.Core;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// The ball of the original scene. Bounce angles, speeds and the score come from the
/// engine-free rules in RetroRally.Core; velocities are in units per second.
/// </summary>
public class Ball : MonoBehaviour
{
    [SerializeField] Text scorePlayer1;
    [SerializeField] Text scorePlayer2;
    [SerializeField] float delayToStartGame = 2f;
    [SerializeField] int pointsToWin = 5;

    MatchScore score;
    int hits;
    Rigidbody2D body;

    void Start()
    {
        body = GetComponent<Rigidbody2D>();
        score = new MatchScore(pointsToWin);
        ShowScore();
        StartCoroutine(Serve(Side.Right));
    }

    void OnCollisionEnter2D(Collision2D collision)
    {
        if (collision.gameObject.CompareTag("LP1"))
        {
            Concede(Side.Left);
        }
        else if (collision.gameObject.CompareTag("LP2"))
        {
            Concede(Side.Right);
        }
        else if (collision.gameObject.CompareTag("Pallet"))
        {
            Return(collision);
        }
    }

    void Concede(Side side)
    {
        bool won = score.Award(side.Other());
        ShowScore();
        if (won)
        {
            SceneManager.LoadScene(2);
            return;
        }
        StopAllCoroutines();
        StartCoroutine(Serve(side));
    }

    void Return(Collision2D collision)
    {
        Side side = collision.transform.position.x < 0f ? Side.Left : Side.Right;
        hits++;
        Vec2 velocity = BallPhysics.Rebound(side, collision.transform.position.y, collision.collider.bounds.extents.y,
            transform.position.y, 0f, hits, out _);
        body.linearVelocity = new Vector2(velocity.X, velocity.Y);
    }

    void ShowScore()
    {
        scorePlayer1.text = score.Of(Side.Left).ToString();
        scorePlayer2.text = score.Of(Side.Right).ToString();
    }

    /// <summary>Waits, then serves from the centre towards <paramref name="towards"/>.</summary>
    IEnumerator Serve(Side towards)
    {
        transform.position = new Vector2(-1f, 0f);
        hits = 0;
        body.linearVelocity = Vector2.zero;
        yield return new WaitForSeconds(delayToStartGame);
        body.linearVelocity = new Vector2(-towards.Facing() * Tuning.ServeSpeed, 0f);
    }
}
