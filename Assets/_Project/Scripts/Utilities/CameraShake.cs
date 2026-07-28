using UnityEngine;

namespace FarmFuryArcade.Utilities
{
    /// <summary>Attach to Main Camera. A brief random jitter — used for GroundSlamAbility's
    /// feedback. Runs in LateUpdate, after CameraFollow's default-order LateUpdate ([DefaultExecutionOrder(100)]
    /// makes this run second), and adds its offset on top of whatever base position CameraFollow
    /// just set rather than caching an absolute "resting" position — with a moving follow camera
    /// there is no fixed resting position to return to; each frame CameraFollow already re-derives
    /// the correct base position, so once the shake timer ends this component simply stops adding
    /// anything, with nothing to explicitly reset.</summary>
    [DefaultExecutionOrder(100)]
    public class CameraShake : Singleton<CameraShake>
    {
        private float _timer;
        private float _magnitude;

        public void Shake(float duration, float magnitude)
        {
            _timer = duration;
            _magnitude = magnitude;
        }

        private void LateUpdate()
        {
            if (_timer <= 0f)
            {
                return;
            }

            _timer -= Time.deltaTime;
            if (_timer <= 0f)
            {
                return;
            }

            Vector2 offset = Random.insideUnitCircle * _magnitude;
            transform.position += new Vector3(offset.x, offset.y, 0f);
        }
    }
}
