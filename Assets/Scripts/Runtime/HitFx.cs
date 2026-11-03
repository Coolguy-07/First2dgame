using UnityEngine;

// One-shot GPU particle bursts for sword hits, deaths and slams. Uses the
// project's soft spark sprite and an HDR material so bloom picks it up.
public static class HitFx
{
    public static void Burst(Vector3 position, Color color, int count, float speed,
                             float size, float lifetime, Material material, int order = 25)
    {
        var go = new GameObject("HitBurst");
        go.transform.position = position;

        var ps = go.AddComponent<ParticleSystem>();
        var main = ps.main;
        main.loop = false;
        main.playOnAwake = false;
        main.duration = 0.18f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(lifetime * 0.55f, lifetime);
        main.startSpeed = new ParticleSystem.MinMaxCurve(speed * 0.45f, speed);
        main.startSize = new ParticleSystem.MinMaxCurve(size * 0.55f, size);
        main.startColor = new ParticleSystem.MinMaxGradient(color, color * 0.85f);
        main.gravityModifier = 0.4f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = Mathf.Max(8, count * 3);
        main.stopAction = ParticleSystemStopAction.Destroy;

        var emission = ps.emission;
        emission.enabled = true;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)Mathf.Max(1, count)) });

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = 0.09f;

        var fade = new Gradient();
        fade.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(color, 1f) },
            new[]
            {
                new GradientAlphaKey(1f, 0f),
                new GradientAlphaKey(0.85f, 0.4f),
                new GradientAlphaKey(0f, 1f)
            });
        var col = ps.colorOverLifetime;
        col.enabled = true;
        col.color = new ParticleSystem.MinMaxGradient(fade);

        var r = go.GetComponent<ParticleSystemRenderer>();
        r.renderMode = ParticleSystemRenderMode.Billboard;
        if (material != null) r.sharedMaterial = material;
        r.sortingOrder = order;
        r.maxParticleSize = 0.6f;

        ps.Play();
        Object.Destroy(go, lifetime + 1.5f);
    }

    public static void SwordSpark(Vector3 position, Material material)
    {
        Burst(position, new Color(1f, 0.85f, 0.45f), 12, 3.4f, 0.14f, 0.42f, material, 26);
    }

    public static void DeathBurst(Vector3 position, Material material, bool heavy)
    {
        Color c = heavy ? new Color(0.95f, 0.45f, 0.2f) : new Color(0.72f, 1f, 0.55f);
        Burst(position, c, heavy ? 30 : 18, heavy ? 4.6f : 3.2f,
              heavy ? 0.22f : 0.15f, heavy ? 0.9f : 0.65f, material, 24);
    }
}
