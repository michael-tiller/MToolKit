using MToolKit.Runtime.Persistence.ES3Integration;
using UnityEngine;
using UnityEngine.Scripting;

namespace ES3Types
{
  [Preserve]
  [ES3Properties("Position", "Rotation", "SceneNumber")]
  public class ES3Type_ExamplePlayerData : ES3Type
  {
    public static ES3Type Instance;

    public ES3Type_ExamplePlayerData() : base(typeof(ExamplePlayerData))
    {
      Instance = this;
    }

    public override void Write(object obj, ES3Writer writer)
    {
      ExamplePlayerData casted = (ExamplePlayerData)obj;
      writer.WriteProperty("Position", casted.Position, ES3Type_Vector3.Instance);
      writer.WriteProperty("Rotation", casted.Rotation, ES3Type_Quaternion.Instance);
      writer.WriteProperty("SceneNumber", casted.SceneNumber, ES3Type_int.Instance);
    }

    public override object Read<T>(ES3Reader reader)
    {
      return new ExamplePlayerData(
        reader.ReadProperty<Vector3>(ES3Type_Vector3.Instance),
        reader.ReadProperty<Quaternion>(ES3Type_Quaternion.Instance),
        reader.ReadProperty<int>(ES3Type_int.Instance)
        );
    }
  }
}