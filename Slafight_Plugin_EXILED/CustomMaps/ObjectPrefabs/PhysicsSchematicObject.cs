using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using AdminToys;
using Exiled.API.Features;
using MEC;
using ProjectMER.Features.Objects;
using Slafight_Plugin_EXILED.API.Features;
using UnityEngine;

namespace Slafight_Plugin_EXILED.CustomMaps.ObjectPrefabs;

public class PhysicsSchematicObject : ObjectPrefab
{
    private const string SchematicOption = "Schematic";
    private const string MassOption = "Mass";
    private const string UseGravityOption = "UseGravity";
    private const string IsKinematicOption = "IsKinematic";
    private const string DetachBlocksOption = "DetachBlocks";
    private const string SyncIntervalOption = "SyncInterval";
    private const string InitialVelocityOption = "InitialVelocity";
    private const string ConstraintsOption = "Constraints";

    private readonly List<PhysicsBlock> _physicsBlocks = [];
    private SchematicObject? _schematicObject;
    private CoroutineHandle _syncCoroutine;
    private bool _physicsEnabled;
    private bool _isCreated;

    /// <summary>
    /// 物理化するスキマティック名（Option: Schematic）。
    /// 基底の宣言的 SchematicName とは別管理（物理有効化を伴う独自スポーンのため）。
    /// </summary>
    public string TargetSchematicName { get; set; } = string.Empty;
    public float Mass { get; set; } = 1f;
    public bool UseGravity { get; set; } = true;
    public bool IsKinematic { get; set; } = false;
    public bool DetachBlocks { get; set; } = true;
    public float SyncInterval { get; set; } = 0.05f;
    public Vector3 InitialVelocity { get; set; } = Vector3.zero;
    public RigidbodyConstraints Constraints { get; set; } = RigidbodyConstraints.None;

    protected override void OnCreate()
    {
        _isCreated = true;
        RecreateSchematic();
    }

    protected override void OnDestroy()
    {
        _isCreated = false;
        DestroyPhysicsSchematic();
        base.OnDestroy();
    }

    protected override void OnTransformUpdated()
    {
        if (!_physicsEnabled)
            base.OnTransformUpdated();
    }

    public override Dictionary<string, string> CollectOptions()
    {
        return new Dictionary<string, string>
        {
            [SchematicOption] = TargetSchematicName,
            [MassOption] = Mass.ToString(CultureInfo.InvariantCulture),
            [UseGravityOption] = UseGravity.ToString(),
            [IsKinematicOption] = IsKinematic.ToString(),
            [DetachBlocksOption] = DetachBlocks.ToString(),
            [SyncIntervalOption] = SyncInterval.ToString(CultureInfo.InvariantCulture),
            [InitialVelocityOption] = FormatVector(InitialVelocity),
            [ConstraintsOption] = Constraints.ToString(),
        };
    }

    public override void ApplyOptions(Dictionary<string, string> options)
    {
        foreach (KeyValuePair<string, string> pair in options)
        {
            string key = NormalizeOptionKey(pair.Key);
            string value = pair.Value;

            switch (key)
            {
                case "schematic":
                case "schematicname":
                case "name":
                    TargetSchematicName = value;
                    break;
                case "mass":
                    if (TryParseFloat(value, out float mass) && mass > 0f)
                        Mass = mass;
                    break;
                case "usegravity":
                case "gravity":
                    if (TryParseBool(value, out bool useGravity))
                        UseGravity = useGravity;
                    break;
                case "iskinematic":
                case "kinematic":
                    if (TryParseBool(value, out bool isKinematic))
                        IsKinematic = isKinematic;
                    break;
                case "detach":
                case "detachblocks":
                    if (TryParseBool(value, out bool detachBlocks))
                        DetachBlocks = detachBlocks;
                    break;
                case "syncinterval":
                case "sync":
                    if (TryParseFloat(value, out float syncInterval) && syncInterval > 0f)
                        SyncInterval = syncInterval;
                    break;
                case "initialvelocity":
                case "velocity":
                    if (TryParseVector(value, out Vector3 velocity))
                        InitialVelocity = velocity;
                    break;
                case "constraints":
                    if (TryParseConstraints(value, out RigidbodyConstraints constraints))
                        Constraints = constraints;
                    break;
            }
        }

        if (_isCreated)
            RecreateSchematic();
    }

    public override bool HandleModCommand(ArraySegment<string> args, out string response)
    {
        if (args.Count < 2)
        {
            response = string.Empty;
            return false;
        }

        switch (args.At(1).ToLowerInvariant())
        {
            case "wake":
            case "wakeup":
                foreach (PhysicsBlock block in _physicsBlocks)
                    block.Rigidbody.WakeUp();

                response = $"Woke up {_physicsBlocks.Count} physics blocks.";
                return true;
            case "sleep":
                foreach (PhysicsBlock block in _physicsBlocks)
                    block.Rigidbody.Sleep();

                response = $"Put {_physicsBlocks.Count} physics blocks to sleep.";
                return true;
            case "impulse":
                if (args.Count < 5 ||
                    !TryParseFloat(args.At(2), out float x) ||
                    !TryParseFloat(args.At(3), out float y) ||
                    !TryParseFloat(args.At(4), out float z))
                {
                    response = "Usage: .sl objprefab mod impulse <x> <y> <z>";
                    return true;
                }

                Vector3 impulse = new(x, y, z);
                foreach (PhysicsBlock block in _physicsBlocks)
                    block.Rigidbody.AddForce(impulse, ForceMode.Impulse);

                response = $"Applied impulse ({FormatVector(impulse)}) to {_physicsBlocks.Count} physics blocks.";
                return true;
            default:
                response = string.Empty;
                return false;
        }
    }

    private void RecreateSchematic()
    {
        DestroyPhysicsSchematic();

        if (string.IsNullOrWhiteSpace(TargetSchematicName))
        {
            Log.Warn("[PhysicsSchematicObject] Schematic option is empty. Use Schematic=<schematicName>.");
            return;
        }

        _schematicObject = SpawnManagedSchematic(TargetSchematicName);
        if (_schematicObject == null)
        {
            Log.Warn($"[PhysicsSchematicObject] Failed to spawn schematic '{TargetSchematicName}'.");
            return;
        }

        EnablePhysics(_schematicObject);
    }

    private void DestroyPhysicsSchematic()
    {
        if (_syncCoroutine.IsRunning)
            Timing.KillCoroutines(_syncCoroutine);

        _syncCoroutine = default;
        _physicsEnabled = false;

        List<GameObject> detachedBlocks = _physicsBlocks
            .Select(block => block.GameObject)
            .Where(block => block != null)
            .Distinct()
            .ToList();

        _physicsBlocks.Clear();
        _schematicObject = null;

        DestroyManagedSchematic();

        foreach (GameObject block in detachedBlocks)
        {
            if (block != null)
                UnityEngine.Object.Destroy(block);
        }
    }

    private void EnablePhysics(SchematicObject schematic)
    {
        _physicsBlocks.Clear();

        foreach (GameObject block in schematic.AttachedBlocks.Where(block => block != null))
        {
            Collider[] colliders = block.GetComponentsInChildren<Collider>(true);
            if (colliders.Length == 0)
                continue;

            if (DetachBlocks)
                block.transform.SetParent(null, true);

            Rigidbody rigidbody = block.GetComponent<Rigidbody>() ?? block.AddComponent<Rigidbody>();
            ApplyRigidbodyOptions(rigidbody);

            AdminToyBase[] toys = block.GetComponentsInChildren<AdminToyBase>(true);
            foreach (AdminToyBase toy in toys)
            {
                toy.NetworkIsStatic = false;
                toy.NetworkMovementSmoothing = 0;
                SyncToy(toy);
            }

            if (InitialVelocity != Vector3.zero)
                rigidbody.linearVelocity = Rotation * InitialVelocity;

            rigidbody.WakeUp();
            _physicsBlocks.Add(new PhysicsBlock(block, rigidbody, toys));
        }

        _physicsEnabled = true;
        if (_physicsBlocks.Count > 0)
            _syncCoroutine = Timing.RunCoroutine(SyncPhysicsBlocks());

        Log.Info($"[PhysicsSchematicObject] Enabled physics for {_physicsBlocks.Count} blocks in '{TargetSchematicName}'.");
    }

    private void ApplyRigidbodyOptions()
    {
        foreach (PhysicsBlock block in _physicsBlocks)
            ApplyRigidbodyOptions(block.Rigidbody);
    }

    private void ApplyRigidbodyOptions(Rigidbody rigidbody)
    {
        rigidbody.mass = Mass;
        rigidbody.useGravity = UseGravity;
        rigidbody.isKinematic = IsKinematic;
        rigidbody.constraints = Constraints;
        rigidbody.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        rigidbody.interpolation = RigidbodyInterpolation.Interpolate;
    }

    private IEnumerator<float> SyncPhysicsBlocks()
    {
        while (_physicsBlocks.Count > 0)
        {
            foreach (PhysicsBlock block in _physicsBlocks.ToList())
            {
                if (block.GameObject == null)
                {
                    _physicsBlocks.Remove(block);
                    continue;
                }

                foreach (AdminToyBase toy in block.Toys)
                    SyncToy(toy);
            }

            yield return Timing.WaitForSeconds(SyncInterval);
        }
    }

    private static void SyncToy(AdminToyBase toy)
    {
        if (toy == null)
            return;

        Transform transform = toy.transform;
        toy.NetworkPosition = transform.position;
        toy.NetworkRotation = transform.rotation;
        toy.NetworkScale = transform.lossyScale;
    }

    private static string NormalizeOptionKey(string key)
        => key.Replace("_", string.Empty).Replace("-", string.Empty).ToLowerInvariant();

    private static bool TryParseFloat(string value, out float result)
        => float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out result);

    private static bool TryParseBool(string value, out bool result)
    {
        if (bool.TryParse(value, out result))
            return true;

        switch (value.Trim().ToLowerInvariant())
        {
            case "1":
            case "yes":
            case "y":
            case "on":
                result = true;
                return true;
            case "0":
            case "no":
            case "n":
            case "off":
                result = false;
                return true;
            default:
                result = false;
                return false;
        }
    }

    private static bool TryParseVector(string value, out Vector3 result)
    {
        result = Vector3.zero;
        string[] parts = value.Split(',', ';', ':');
        if (parts.Length != 3)
            return false;

        if (!TryParseFloat(parts[0], out float x) ||
            !TryParseFloat(parts[1], out float y) ||
            !TryParseFloat(parts[2], out float z))
        {
            return false;
        }

        result = new Vector3(x, y, z);
        return true;
    }

    private static bool TryParseConstraints(string value, out RigidbodyConstraints constraints)
    {
        if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int numeric))
        {
            constraints = (RigidbodyConstraints)numeric;
            return true;
        }

        return Enum.TryParse(value, true, out constraints);
    }

    private static string FormatVector(Vector3 value)
        => $"{value.x.ToString(CultureInfo.InvariantCulture)},{value.y.ToString(CultureInfo.InvariantCulture)},{value.z.ToString(CultureInfo.InvariantCulture)}";

    private readonly struct PhysicsBlock
    {
        public PhysicsBlock(GameObject gameObject, Rigidbody rigidbody, AdminToyBase[] toys)
        {
            GameObject = gameObject;
            Rigidbody = rigidbody;
            Toys = toys;
        }

        public GameObject GameObject { get; }
        public Rigidbody Rigidbody { get; }
        public AdminToyBase[] Toys { get; }
    }
}
