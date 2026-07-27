using UnityEngine;

namespace FarmFuryArcade.Utilities
{
    /// <summary>Attach to Main Camera. A brief random jitter around its resting local position —
    /// used for GroundSlamAbility's feedback.</summary>
    public class CameraShake : Singleton<CameraShake>
    {
        private Vector3 _restingLocalPosition;
        private float _timer;
        private float _magnitude;

        protected override void Awake()
        {
            base.Awake();
            _restingLocalPosition = transform.localPosition;
        }

        public void Shake(float duration, float magnitude)
        {
            _timer = duration;
            _magnitude = magnitude;
        }

        private void Update()
        {
            if (_timer <= 0f)
            {
                return;
            }

            _timer -= Time.deltaTime;
            if (_timer <= 0f)
            {
                transform.localPosition = _restingLocalPosition;
                return;
            }

            Vector2 offset = Random.insideUnitCircle * _magnitude;
            transform.localPosition = _restingLocalPosition + new Vector3(offset.x, offset.y, 0f);
        }
    }
}
