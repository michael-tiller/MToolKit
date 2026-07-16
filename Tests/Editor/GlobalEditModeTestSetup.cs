using NUnit.Framework;

/// <summary>
/// Assembly-global (no-namespace) NUnit <see cref="SetUpFixtureAttribute"/> for the
/// MToolKit.Tests.Editor run.
///
/// Serilog's static <c>Log.Logger</c> survives PlayMode EXIT (Unity reloads the domain on play
/// ENTER and on recompile, never on exit), so an EditMode run that follows a PlayMode session in
/// a consuming project inherits the game's SlogConfig pipeline — including the Unity3D console
/// sink. Tests that deliberately provoke a <c>log.Error(...)</c> (GraphRunner executor-throws,
/// GraphStateSaveController export-failure isolation, …) then fail with "Unhandled log message"
/// (observed 2026-06-11 from Dirigible's runner, 105/4828 across three test assemblies).
/// <c>CloseAndFlush</c> disposes that pipeline and resets <c>Log.Logger</c> to the silent
/// default — the baseline these characterization tests were written against. Fixtures that need
/// a real logger swap one in per-test via <c>SerilogSinkScope</c> and restore on dispose, which
/// composes cleanly with a silent baseline.
/// </summary>
[SetUpFixture]
public class MToolKitGlobalEditModeTestSetup
{
    [OneTimeSetUp]
    public void OneTimeSetUp()
    {
        Serilog.Log.CloseAndFlush();
    }
}
