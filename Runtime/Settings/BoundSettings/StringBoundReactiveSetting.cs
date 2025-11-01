namespace MToolKit.Runtime.Settings.BoundSettings
{
  public class StringBoundReactiveSetting : AbstractBoundReactiveSetting<string>
  {
    public override ReactiveSetting<string> Setting { get; set; }
    public override void Bind(ReactiveSetting<string> reactiveSetting)
    {
      Setting = reactiveSetting;
    }
  }
}