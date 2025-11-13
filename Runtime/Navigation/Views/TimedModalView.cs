// Navigation/Views/TimedModalView.cs

using System.Collections;
using MToolKit.Runtime.Navigation.DataStructures;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace MToolKit.Runtime.Navigation.Views
{
  public class TimedModalView : ModalView
  {
    [SerializeField]
    [Tooltip("Timeout duration in seconds")]
    private float timeoutDuration = 5f;

    [SerializeField]
    [Required]
    [Tooltip("Slider displaying timeout progress")]
    private Slider timeoutSlider;

    private UnityAction timeoutCallback;
    private Coroutine timeoutCoroutine;

    /// <summary>
    ///   Initializes the Timed Modal with an additional timeout callback and displays a timeout progress slider.
    /// </summary>
    /// <param name="title">The title text of the modal.</param>
    /// <param name="message">The message text of the modal.</param>
    /// <param name="button1Config">Button 1 Config.</param>
    /// <param name="button2Config">Button 2 Config (optional).</param>
    /// <param name="button3Config">Button 3 Config (optional).</param>
    /// <param name="timeout">Optional override for timeout duration (seconds).</param>
    /// <param name="timeoutCallbackAction">The callback executed when timeout occurs.</param>
    public void Initialize(
      string title,
      string message,
      ModalButtonConfig button1Config, ModalButtonConfig button2Config = default, ModalButtonConfig button3Config = default,
      float? timeout = null, UnityAction timeoutCallbackAction = null)
    {
      // Initialize the modal with provided button setups.
      base.Initialize(title, message, button1Config, button2Config, button3Config);

      // Override timeout if specified.
      if (timeout.HasValue)
        timeoutDuration = timeout.Value;

      timeoutCallback = timeoutCallbackAction;

      // Initialize the slider if available.
      if (timeoutSlider != null)
      {
        timeoutSlider.minValue = 0f;
        timeoutSlider.maxValue = 1f;
        timeoutSlider.value = 1f;
        timeoutSlider.gameObject.SetActive(true);
      }

      // Start the timeout coroutine if a valid timeout and callback are provided.
      if (timeoutDuration > 0 && timeoutCallback != null)
      {
        if (timeoutCoroutine != null)
          StopCoroutine(timeoutCoroutine);
        timeoutCoroutine = StartCoroutine(TimeoutRoutine());
      }
    }

    private IEnumerator TimeoutRoutine()
    {
      float elapsedTime = 0f;
      while (elapsedTime < timeoutDuration)
      {
        elapsedTime += Time.deltaTime;
        if (timeoutSlider != null)
          // Update slider value: from full (1) to empty (0).
          timeoutSlider.value = 1f - elapsedTime / timeoutDuration;
        yield return null;
      }
      timeoutCallback?.Invoke();
      Hide(); // Optionally hide the modal after timeout.
    }

    public override void Hide()
    {
      if (timeoutCoroutine != null)
      {
        StopCoroutine(timeoutCoroutine);
        timeoutCoroutine = null;
      }
      base.Hide();
    }
  }
}