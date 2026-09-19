// File: UnityStubs.cs
// Purpose: Compile-check the Unity adapter boundary in ordinary .NET CI without redistributing Unity.
// Public API: Minimal UnityEngine type stubs enabled only in the contract-test project.
// Variables: Attributes and logging methods have no runtime behavior.
// Declaration index: docs/CODE_INDEX.md contains generated, exact line locations.

#if UNITY_CONTRACT_TEST
namespace UnityEngine;

[AttributeUsage(AttributeTargets.Field)]
public sealed class SerializeField : Attribute
{
}

[AttributeUsage(AttributeTargets.Method)]
public sealed class ContextMenu : Attribute
{
    public ContextMenu(string name)
    {
        _ = name;
    }
}

[AttributeUsage(AttributeTargets.Class)]
public sealed class CreateAssetMenuAttribute : Attribute
{
    public string MenuName { get; set; } = string.Empty;
}

public class MonoBehaviour
{
}

public class ScriptableObject
{
}

public static class Debug
{
    public static void Log(object message) => Console.WriteLine(message);
}
#endif
