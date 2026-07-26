using CourtSmasherz;
using UnityEngine;

public class SmashParticleController : MonoBehaviour
{
    [SerializeField] private PickleballRacquetController racket;
    [SerializeField] private RacketForceApplier racketForceApplier;
    [SerializeField] private ParticleSystem fireParticle;
    [SerializeField] private float spawnCooldown = 0.5f;

    private float lastSpawnTime = -Mathf.Infinity;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        if (fireParticle != null)
        {
            fireParticle.Stop(
                true,
                ParticleSystemStopBehavior.StopEmittingAndClear
            );
        }
    }

    // Update is called once per frame
    void Update()
    {
        if (racket == null || fireParticle == null)
            {
                return;
            }

        if (IsSmashDetected())
        {
            lastSpawnTime = Time.time;
            SpawnFire();
        }
    }

    void SpawnFire()
    {
        fireParticle.Stop(
            true,
            ParticleSystemStopBehavior.StopEmittingAndClear
        );

        fireParticle.Play(true);
    }

    bool IsSmashDetected()
    {
        return racket.DetectedShotType == ShotType.Smash
            && Time.time >= lastSpawnTime + spawnCooldown
            && racket.HasRecentDetectedShotType(0.1f);
    }
}
