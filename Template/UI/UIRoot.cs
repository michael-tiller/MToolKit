
using Sirenix.OdinInspector;
using UnityEngine;

namespace MToolKit.Template.UI
{
  public class UIRoot : MonoBehaviour
  {
    [field: SerializeField][Required] public Canvas MainCanvas { get; private set; }
    [field: SerializeField][Required] public GameOverPanel GameOverPanel { get; private set; }
    [field: SerializeField][Required] public PausePanel PausePanel { get; private set; }
    [field: SerializeField][Required] public BlackoutPanel BlackoutPanel { get; private set; }
  }
}