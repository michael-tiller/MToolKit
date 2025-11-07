// Navigation/Views/TimedModalView.cs

using System.Collections;
using MToolKit.Runtime.Navigation.DataStructures;
using MToolKit.Runtime.Settings.Enums;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace MToolKit.Runtime.Navigation.Views
{
    public class TimedModalView : ModalView
    {
        [SerializeField, Tooltip("Timeout duration in seconds")]
        private float timeoutDuration = 5f;

        [SerializeField, Required, Tooltip("Slider displaying timeout progress")]
        private Slider timeoutSlider;

        private UnityAction timeoutCallback;
        private Coroutine timeoutCoroutine;

        /// <summary>
        /// Initializes the Timed Modal with an additional timeout callback and displays a timeout progress slider.
        /// </summary>
        /// <param name="title">The title text of the modal.</param>
        /// <param name="message">The message text of the modal.</param>
        /// <param name="type1">Button 1 type.</param>
        /// <param name="text1">Button 1 text.</param>
        /// <param name="action1">Button 1 callback.</param>
        /// <param name="type2">Button 2 type (optional).</param>
        /// <param name="text2">Button 2 text (optional).</param>
        /// <param name="action2">Button 2 callback (optional).</param>
        /// <param name="type3">Button 3 type (optional).</param>
        /// <param name="text3">Button 3 text (optional).</param>
        /// <param name="action3">Button 3 callback (optional).</param>
        /// <param name="timeout">Optional override for timeout duration (seconds).</param>
        /// <param name="timeoutCallback">The callback executed when timeout occurs.</param>
        public void Initialize(
            string title,
            string message,
            ModalButtonConfig button1, ModalButtonConfig button2 = default, ModalButtonConfig button3 = default,
            float? timeout = null, UnityAction timeoutCallback = null)
        {
            // Initialize the modal with provided button setups.
            base.Initialize(title, message, button1, button2, button3);

            // Override timeout if specified.
            if (timeout.HasValue)
            {
                timeoutDuration = timeout.Value;
            }

            this.timeoutCallback = timeoutCallback;

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
                {
                    StopCoroutine(timeoutCoroutine);
                }
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
                {
                    // Update slider value: from full (1) to empty (0).
                    timeoutSlider.value = 1f - (elapsedTime / timeoutDuration);
                }
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
