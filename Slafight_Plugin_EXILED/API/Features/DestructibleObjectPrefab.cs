using System;
using System.Linq;
using ProjectMER.Events.Arguments;
using ProjectMER.Features.Enums;
using ProjectMER.Features.Objects;
using UnityEngine;

namespace Slafight_Plugin_EXILED.API.Features;

/// <summary>
/// ProjectMER CustomHitbox を耐久値付き ObjectPrefab として扱う基底クラス。
/// Unity側マーカーが無いSchematicでも、Interactable以外のColliderから互換Hitboxを構成する。
/// </summary>
public abstract class DestructibleObjectPrefab : ObjectPrefab
{
    private SchematicCustomHitbox? _customHitbox;

    protected virtual string CustomHitboxGroup => "Main";
    protected virtual string CustomHitboxTag => string.Empty;
    protected virtual float CustomHitboxDamageMultiplier => 1f;
    protected virtual bool CreateFallbackCustomHitbox => true;

    public virtual float Durability { get; set; } = 100f;
    public float RemainingDurability => _customHitbox?.Health ?? Math.Max(0f, Durability);
    protected SchematicCustomHitbox? CustomHitbox => _customHitbox;

    protected override bool IsAutomaticOptionProperty(System.Reflection.PropertyInfo property) =>
        property.Name != nameof(RemainingDurability) && base.IsAutomaticOptionProperty(property);

    protected sealed override void OnSetup()
    {
        BindCustomHitbox();
        OnDestructibleSetup();
    }

    protected virtual void OnDestructibleSetup() { }

    protected virtual bool IsFallbackHitboxBlock(SchematicBlock block) =>
        block.BlockType != BlockType.Interactable &&
        block.GetComponentsInChildren<Collider>(true).Any(collider =>
            collider != null && collider.GetComponentInParent<SchematicBlock>() == block);

    protected virtual void OnCustomHitboxDamaged(CustomHitboxDamagedEventArgs ev) { }

    protected virtual void OnDurabilityDepleted(CustomHitboxDiedEventArgs ev) => Destroy();

    protected override void OnDestroy()
    {
        UnbindCustomHitbox();
        base.OnDestroy();
    }

    protected override void OnRoundRestarting()
    {
        UnbindCustomHitbox();
        base.OnRoundRestarting();
    }

    private void BindCustomHitbox()
    {
        if (Schematic == null)
            return;

        _customHitbox = SchematicCustomHitbox.Get(Schematic, CustomHitboxGroup);
        if (_customHitbox == null && CreateFallbackCustomHitbox)
        {
            _customHitbox = SchematicCustomHitbox.Create(
                Schematic,
                CustomHitboxGroup,
                Schematic.Blocks.Where(IsFallbackHitboxBlock),
                Durability,
                CustomHitboxDamageMultiplier,
                CustomHitboxTag);
        }

        if (_customHitbox == null)
            return;

        _customHitbox.SetMaxHealth(Durability);
        _customHitbox.SetDamageMultiplier(CustomHitboxDamageMultiplier);
        _customHitbox.Damaged += OnCustomHitboxDamaged;
        _customHitbox.Died += OnDurabilityDepleted;
    }

    private void UnbindCustomHitbox()
    {
        if (_customHitbox != null)
        {
            _customHitbox.Damaged -= OnCustomHitboxDamaged;
            _customHitbox.Died -= OnDurabilityDepleted;
        }

        _customHitbox = null;
    }
}
