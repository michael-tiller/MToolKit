using MToolKit.Runtime.ErrorSystem.Messages;

namespace MToolKit.Runtime.ErrorSystem.Interface
{
  /// <summary>
  ///   Service interface for displaying error messages to the user.
  ///   Provides methods to show and hide error dialogs.
  /// </summary>
  public interface IErrorService
  {
    ErrorRequestMessage? LastErrorMessage { get; }

    /// <summary>
    ///   Shows an error message to the user.
    /// </summary>
    /// <param name="message">The error message to display</param>
    void ShowError(ErrorRequestMessage message);

    /// <summary>
    ///   Hides the currently displayed error message.
    /// </summary>
    void HideError();
  }
}