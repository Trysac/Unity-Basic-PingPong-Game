using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace RetroRally.Tests
{
    /// <summary>The serve in the real Game scene travels at the same speed at any frame rate.</summary>
    public class ServeSpeedTests
    {
        float measured;

        [TearDown]
        public void ResetFrameRate() => Time.captureFramerate = 0;

        IEnumerator MeasureServe(int framesPerSecond)
        {
            Time.captureFramerate = framesPerSecond;
            SceneManager.LoadScene("Game");
            yield return null;
            var ball = GameObject.Find("Ball").GetComponent<Rigidbody2D>();
            int frames = 0;
            while (ball.linearVelocity.sqrMagnitude < 0.0001f && frames < framesPerSecond * 5)
            {
                frames++;
                yield return null;
            }
            measured = ball.linearVelocity.magnitude;
            Assert.That(measured, Is.GreaterThan(0f), "the ball was never served");
        }

        [UnityTest]
        public IEnumerator ServeSpeedDoesNotDependOnTheFrameRate()
        {
            yield return MeasureServe(30);
            float slow = measured;
            yield return MeasureServe(120);
            float fast = measured;
            Assert.That(fast, Is.EqualTo(slow).Within(0.01f * slow),
                $"serve speed was {slow:0.00} u/s at 30 fps and {fast:0.00} u/s at 120 fps");
        }
    }
}
