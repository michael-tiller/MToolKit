using System;
using System.Collections.Generic;
using UnityEngine;

namespace MToolKit.Editor.SpriteAuthoring
{
  public readonly struct SpriteRename
  {
    public Sprite SourceSprite { get; }
    public string NewName { get; }
    public SpriteRename(Sprite sourceSprite, string newName)
    { SourceSprite = sourceSprite; NewName = newName; }
  }

  /// <summary>Optional native 2D Sprite Editor authoring; players do not depend on this assembly.</summary>
  public static class SpriteRenameUtility
  {
    public static int Rename(IReadOnlyList<SpriteRename> renames)
      => throw new NotImplementedException("Native sprite renaming is not implemented yet.");
  }
}
