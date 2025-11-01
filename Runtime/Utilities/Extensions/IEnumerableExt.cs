using System.Collections.Generic;
using System.Linq;

namespace MToolKit.Runtime.Utilities.Extensions
{
  public static class IEnumerableExt
  {
    public static bool IsNullOrEmpty<T>(this IEnumerable<T> enumerable)
    {
      return (enumerable == null || !enumerable.Any());
    }
  }

}