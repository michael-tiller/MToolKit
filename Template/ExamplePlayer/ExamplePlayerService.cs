using MToolKit.Template.ExamplePlayer.Interface;
using UnityEngine;

namespace MToolKit.Template.ExamplePlayer
{
    /// <summary>
    /// Simple service that holds a reference to the player's transform
    /// </summary>
    public class ExamplePlayerService : IExamplePlayerService
    {
        public Transform PlayerTransform { get; set; }
    }
}
