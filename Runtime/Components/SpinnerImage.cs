using System;
using DG.Tweening;
using Serilog;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.UI;
using ILogger = Serilog.ILogger;
using Logger = Serilog.Core.Logger;


namespace MToolKit.Runtime.Components
{
  [RequireComponent(typeof(Image))]
  public class SpinnerImage : MonoBehaviour
  {
    private static readonly Lazy<ILogger> logLazy = new(() => Log.Logger.ForContext<SpinnerImage>().ForFeature("Components"));
    private static ILogger log => logLazy.Value ?? Logger.None;

    [field: SerializeField]
    [Range(0.1f, 10f)]
    public float Duration { get; private set; } = 1f;

    [field: SerializeField]
    public Ease EaseType { get; private set; } = Ease.Linear;

    [field: SerializeField]
    public bool StartOnAwake { get; private set; } = true;

    [field: SerializeField]
    public bool Clockwise { get; private set; } = true;

    [field: SerializeField]
    [Required]
    public Image Image { get; private set; }

    private Tween rotationTween;

    public bool IsSpinning { get; private set; }

    private void Awake()
    {
      if (StartOnAwake)
        StartSpinning();
    }

    private void Reset()
    {
      Image = GetComponent<Image>();
    }

    private void OnDestroy()
    {
      StopSpinning();
    }

    private void StartSpinning()
    {
      if (IsSpinning) return;

      StopSpinning();

      float rotationDirection = Clockwise ? -360f : 360f;

      rotationTween = transform
        .DORotate(new Vector3(0, 0, rotationDirection), Duration, RotateMode.FastBeyond360)
        .SetEase(EaseType)
        .SetLoops(-1, LoopType.Restart)
        .OnStart(() => IsSpinning = true)
        .OnKill(() => IsSpinning = false);

      log.ForMethod().Debug("Started spinning with duration: {Duration}s, clockwise: {Clockwise}", Duration, Clockwise);
    }

    private void StopSpinning()
    {
      if (rotationTween != null && rotationTween.IsActive())
      {
        rotationTween.Kill();
        rotationTween = null;
      }
      IsSpinning = false;
    }

    public void SetDuration(float newDuration)
    {
      Duration = Mathf.Clamp(newDuration, 0.1f, 10f);

      if (IsSpinning)
        StartSpinning(); // Restart with new duration
    }

    public void SetClockwise(bool clockwise)
    {
      Clockwise = clockwise;

      if (IsSpinning)
        StartSpinning(); // Restart with new direction
    }

    public void SetEaseType(Ease easeType)
    {
      EaseType = easeType;

      if (IsSpinning)
        StartSpinning(); // Restart with new ease type
    }
  }
}