// File: AbilityAsset.cs
// Purpose: Expose data-driven tactical abilities to Unity designers through ScriptableObject assets.
// Public API: AbilityAsset and its ToDefinition conversion method.
// Variables: Serialized fields define identifier, damage, range, and cooldown in the Inspector.
// Declaration index: docs/CODE_INDEX.md contains generated, exact line locations.

using TacticalAI.Core;
using UnityEngine;

namespace TacticalAI.Unity;

/// <summary>Unity authoring asset that converts into a validated portable ability definition.</summary>
[CreateAssetMenu(MenuName = "Tactical AI/Ability")]
public sealed class AbilityAsset : ScriptableObject
{
    [SerializeField]
    private string abilityId = "pulse-shot";

    [SerializeField]
    private int damage = 34;

    [SerializeField]
    private int range = 5;

    [SerializeField]
    private int cooldown = 1;

    /// <summary>Creates the engine-neutral value consumed by the simulation core.</summary>
    public AbilityDefinition ToDefinition() => new(abilityId, damage, range, cooldown);
}
