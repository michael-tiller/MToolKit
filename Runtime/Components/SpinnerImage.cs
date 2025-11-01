using System;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;
using Serilog;
using ILogger = Serilog.ILogger;
using Sirenix.OdinInspector;


namespace MToolKit.Runtime.Components
{
    [RequireComponent(typeof(Image))]
    public class SpinnerImage : MonoBehaviour
    {
        private static readonly Lazy<ILogger> logLazy = new(() => Log.Logger.ForContext<SpinnerImage>().ForFeature("Components"));
        private static ILogger log => logLazy.Value ?? Serilog.Core.Logger.None;

        [field: SerializeField] [Range(0.1f, 10f)] public float Duration { get; private set; } = 1f;
        [field: SerializeField] public Ease EaseType { get; private set; } = Ease.Linear;
        [field: SerializeField] public bool StartOnAwake { get; private set; } = true;
        [field: SerializeField] public bool Clockwise { get; private set; } = true;

        private Tween rotationTween;
        private bool isSpinning;

        [field: SerializeField] [Required] public Image Image { get; private set; } = null;

        private void Reset()
        {
            Image = GetComponent<Image>();
        }

        private void Awake()
        {
            if (StartOnAwake)
            {
                StartSpinning();
            }
        }

        private void OnDestroy()
        {
            StopSpinning();
        }

        public void StartSpinning()
        {
            if (isSpinning) return;

            StopSpinning();

            float rotationDirection = Clockwise ? -360f : 360f;
            
            rotationTween = transform
                .DORotate(new Vector3(0, 0, rotationDirection), Duration, RotateMode.FastBeyond360)
                .SetEase(EaseType)
                .SetLoops(-1, LoopType.Restart)
                .OnStart(() => isSpinning = true)
                .OnKill(() => isSpinning = false);

            log.ForMethod(nameof(StartSpinning)).Debug("Started spinning with duration: {Duration}s, clockwise: {Clockwise}", Duration, Clockwise);
        }

        public void StopSpinning()
        {
            if (rotationTween != null && rotationTween.IsActive())
            {
                rotationTween.Kill();
                rotationTween = null;
            }
            isSpinning = false;
        }

        public void SetDuration(float newDuration)
        {
            Duration = Mathf.Clamp(newDuration, 0.1f, 10f);
            
            if (isSpinning)
            {
                StartSpinning(); // Restart with new duration
            }
        }

        public void SetClockwise(bool clockwise)
        {
            Clockwise = clockwise;
            
            if (isSpinning)
            {
                StartSpinning(); // Restart with new direction
            }
        }

        public void SetEaseType(Ease easeType)
        {
            EaseType = easeType;
            
            if (isSpinning)
            {
                StartSpinning(); // Restart with new ease type
            }
        }

        public bool IsSpinning => isSpinning;
    }
}