using System;

namespace MToolKit.Runtime.Persistence.Interfaces
{
  /// <summary>Preserves a controller's live save binding while an application captures to a temporary profile.</summary>
  public interface IProvisionalSaveCaptureParticipant
  {
    /// <summary>Returns a scope that remains active until the application restores the original profile.</summary>
    /// <remarks>The participant must unwind its own state if entry fails and must return a non-null scope.</remarks>
    IDisposable BeginProvisionalSaveCapture();
  }
}
