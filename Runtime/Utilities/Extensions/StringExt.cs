namespace MToolKit.Runtime.Utilities.Extensions
{
  public static class StringExt
  {
    public static int ConvertToNumber(this string str) => StringToNumberConverter.ConvertStringToNumber(str);
  }

}