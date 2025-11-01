using UnityEngine;

namespace MToolKit.Template.ExamplePlayer.Interface
{
    /// <summary>
    /// Simple service that holds a reference to the player's transform
    /// </summary>
    public interface IExamplePlayerService
    {
        Transform PlayerTransform { get; set; }
    }
}