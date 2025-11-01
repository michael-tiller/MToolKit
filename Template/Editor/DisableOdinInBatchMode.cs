#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace MToolKit.Template.Editor
{
[InitializeOnLoad]
public static class DisableOdinInBatchmode
{
    static DisableOdinInBatchmode()
    {
        if (Application.isBatchMode)
        {
            // Disable Odin Validator background tasks
            System.Environment.SetEnvironmentVariable("ODIN_VALIDATOR_DISABLED", "1");
                EditorPrefs.SetBool("OdinValidator_AutoValidate", false);
            }
        }
    }
}
#endif