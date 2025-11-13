namespace MToolKit.Runtime.Settings.BoundSettings
{
  public class FloatBoundReactiveSetting : AbstractBoundReactiveSetting<float>
  {
    public override ReactiveSetting<float> Setting { get; set; }

    public override void Bind(ReactiveSetting<float> reactiveSetting)
    {
      Setting = reactiveSetting;
    }
  }
}