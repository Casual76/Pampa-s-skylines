#nullable enable

namespace PampaSkylines.PC
{
using UnityEngine;
using UnityEngine.SceneManagement;

public static class PcBootstrapRuntime
{
    public const string ScenePath = "Assets/Game/PC/Scenes/PcBootstrap.unity";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void WarnIfBootstrapControllerIsMissing()
    {
        if (!Application.isPlaying || Object.FindFirstObjectByType<PcBootstrapController>() is not null)
        {
            return;
        }

        var activeScene = SceneManager.GetActiveScene();
        Debug.Log(
            $"No {nameof(PcBootstrapController)} found in scene '{activeScene.name}'. " +
            $"Open '{ScenePath}' to run the PC prototype scene.");
    }
}
}
