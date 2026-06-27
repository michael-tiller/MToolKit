using UnityEngine;

namespace MToolKit.Runtime.Utilities
{
  public class SemanticScriptableObject : ScriptableObject
  {
    [SerializeField]
    private string semId;

    public string SemId => semId;

    public string Id => !string.IsNullOrWhiteSpace(SemId) ? SemId : name;
  }
}