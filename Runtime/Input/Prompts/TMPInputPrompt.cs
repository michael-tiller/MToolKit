using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Localization;
using VContainer;

namespace MToolKit.Runtime.Input.Prompts
{
  /// <summary>A lifecycle-owned native TMP/LocalizedString adapter. Gameplay action enablement is never changed.</summary>
  [DisallowMultipleComponent]
  [RequireComponent(typeof(TMP_Text))]
  public sealed class TMPInputPrompt : MonoBehaviour
  {
    [SerializeField] private TMP_Text target;
    [SerializeField] private InputActionReference action;
    [SerializeField] private InputPromptStyle style;
    [Tooltip("Optional standalone configuration. Injected services own their own configuration/player context.")]
    [SerializeField] private InputPromptSettings standaloneSettings;
    [SerializeField] private LocalizedString sentence = new();
    [SerializeField] private string argumentName = "input";

    private IInputPromptService prompts;
    private InputPromptService ownedPrompts;
    private IDisposable binding;
    private string localizedSentence;
    private string lastPrompt;
    private bool sentenceSubscribed;
    private bool started;

    [Inject]
    public void Construct(IInputPromptService service)
    {
      ReleaseBinding();
      ownedPrompts?.Dispose();
      ownedPrompts = null;
      prompts = service ?? throw new ArgumentNullException(nameof(service));
      if (isActiveAndEnabled) Attach();
    }

    public void Configure(InputActionReference actionReference, InputPromptStyle presentation = InputPromptStyle.Auto)
    {
      action = actionReference;
      style = presentation;
      Refresh();
    }

    public void ConfigureSentence(LocalizedString translatedSentence, string namedArgument = "input")
    {
      if (string.IsNullOrWhiteSpace(namedArgument)) throw new ArgumentException("A sentence argument requires a name.", nameof(namedArgument));
      ReleaseBinding();
      sentence = translatedSentence ?? new LocalizedString();
      argumentName = namedArgument;
      if (isActiveAndEnabled && (prompts != null || started)) Attach();
    }

    public void Refresh()
    {
      ReleaseBinding();
      if (isActiveAndEnabled && (prompts != null || started)) Attach();
    }

    private void Reset() => target = GetComponent<TMP_Text>();
    private void Start() { started = true; if (binding == null) Attach(); }
    private void OnEnable() { if (prompts != null || started) Attach(); }

    private void Attach()
    {
      if (binding != null) return;
      if (target == null) target = GetComponent<TMP_Text>();
      if (prompts == null)
      {
        // Bind owns the current action lease. A standalone component may be
        // reconfigured to another asset without retaining its previous owner.
        ownedPrompts = new InputPromptService(standaloneSettings);
        prompts = ownedPrompts;
      }
      binding = prompts.Bind(target, Render);
      // The first native subscription may format synchronously when a table
      // is cached. Render primes the named arguments before subscribing.
      if (sentence != null && !sentence.IsEmpty)
      {
        try
        {
          sentenceSubscribed = true;
          sentence.StringChanged += OnSentenceChanged;
        }
        catch { ReleaseBinding(); throw; }
      }
    }

    private string Render()
    {
      string prompt = prompts.ResolveReference(action, style);
      if (sentence == null || sentence.IsEmpty) return prompt;
      if (lastPrompt != prompt)
      {
        lastPrompt = prompt;
        sentence.Arguments = new object[] { new Dictionary<string, object> { [argumentName] = prompt } };
        sentence.RefreshString();
      }
      return localizedSentence ?? prompt;
    }

    private void OnSentenceChanged(string value)
    {
      localizedSentence = value;
      if (isActiveAndEnabled && target != null && target.text != value) target.text = value;
    }

    private void ReleaseBinding()
    {
      binding?.Dispose();
      binding = null;
      if (sentenceSubscribed && sentence != null) sentence.StringChanged -= OnSentenceChanged;
      sentenceSubscribed = false;
      lastPrompt = null;
      localizedSentence = null;
    }

    private void OnDisable()
    {
      ReleaseBinding();
      if (ownedPrompts != null)
      {
        ownedPrompts.Dispose();
        ownedPrompts = null;
        prompts = null;
      }
    }

    private void OnDestroy() => OnDisable();
    private void OnValidate() { if (Application.isPlaying) Refresh(); }
  }
}
