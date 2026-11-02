using UnityEngine;

namespace RetroRally.Game
{
    /// <summary>Particle bursts made in code.</summary>
    public static class Effects
    {
        /// <summary>A one-shot spray of glowing particles that cleans itself up.</summary>
        /// <param name="direction">Main direction of the spray; zero sprays all around.</param>
        public static void Burst(Vector2 position, Vector2 direction, Material material, Color colorA, Color colorB,
            int count, float speed, float size, float lifetime, float spread = 70f)
        {
            if (material == null || count <= 0)
            {
                return;
            }
            var host = new GameObject("Burst");
            host.transform.position = new Vector3(position.x, position.y, -0.2f);
            var system = host.AddComponent<ParticleSystem>();
            system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            ParticleSystem.MainModule main = system.main;
            main.duration = 0.1f;
            main.loop = false;
            main.useUnscaledTime = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(lifetime * 0.5f, lifetime);
            main.startSpeed = new ParticleSystem.MinMaxCurve(speed * 0.35f, speed);
            main.startSize = new ParticleSystem.MinMaxCurve(size * 0.5f, size);
            main.startColor = new ParticleSystem.MinMaxGradient(colorA, colorB);
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.stopAction = ParticleSystemStopAction.Destroy;

            ParticleSystem.EmissionModule emission = system.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)count) });

            ParticleSystem.ShapeModule shape = system.shape;
            if (direction.sqrMagnitude > 1e-4f)
            {
                shape.shapeType = ParticleSystemShapeType.Cone;
                shape.angle = spread * 0.5f;
                shape.radius = 0.05f;
                float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
                // The cone points along +Z; turn it into the court's plane along the direction.
                shape.rotation = new Vector3(-angle, 90f, 0f);
            }
            else
            {
                shape.shapeType = ParticleSystemShapeType.Circle;
                shape.radius = 0.1f;
            }

            ParticleSystem.LimitVelocityOverLifetimeModule drag = system.limitVelocityOverLifetime;
            drag.enabled = true;
            drag.drag = 2.5f;

            ParticleSystem.SizeOverLifetimeModule shrink = system.sizeOverLifetime;
            shrink.enabled = true;
            shrink.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0f));

            ParticleSystem.ColorOverLifetimeModule fade = system.colorOverLifetime;
            fade.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.8f, 0.5f), new GradientAlphaKey(0f, 1f) });
            fade.color = gradient;

            var renderer = host.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            renderer.sortingOrder = 20;
            system.Play();
        }
    }
}
