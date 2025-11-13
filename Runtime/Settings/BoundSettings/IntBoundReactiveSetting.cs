namespace MToolKit.Runtime.Settings.BoundSettings
{
  public class IntBoundReactiveSetting : AbstractBoundReactiveSetting<int>
  {
    public override ReactiveSetting<int> Setting { get; set; }

    public override void Bind(ReactiveSetting<int> reactiveSetting)
    {
      Setting = reactiveSetting;
    }
  }
}