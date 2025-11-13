namespace MToolKit.Runtime.Settings.BoundSettings
{
  public class BoolBoundReactiveSetting : AbstractBoundReactiveSetting<bool>
  {
    public override ReactiveSetting<bool> Setting { get; set; }

    public override void Bind(ReactiveSetting<bool> reactiveSetting)
    {
      Setting = reactiveSetting;
    }
  }
}