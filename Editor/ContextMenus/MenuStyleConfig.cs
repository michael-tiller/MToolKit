using System;
using UnityEngine;

namespace MToolKit.Editor.ContextMenus
{
  [CreateAssetMenu(menuName = "MToolKit/Context Menus/Authoring/Style Configuration", fileName = "MenuStyleConfig")]
  public sealed class MenuStyleConfig : ScriptableObject
  {
    public MenuCommonSettings Common;
    public string OutputFolder = "Assets/MToolKit/Menus";
    [SerializeReference] public MenuStyleRecipe[] Styles = Array.Empty<MenuStyleRecipe>();
    [SerializeField, HideInInspector] private GameObject generatedListPresenter;
    [SerializeField, HideInInspector] private GameObject generatedRadialPresenter;
    public GameObject GeneratedListPresenter => generatedListPresenter;
    public GameObject GeneratedRadialPresenter => generatedRadialPresenter;
    internal void SetGeneratedPresenters(GameObject list, GameObject radial)
    { generatedListPresenter = list; generatedRadialPresenter = radial; }
  }
}
