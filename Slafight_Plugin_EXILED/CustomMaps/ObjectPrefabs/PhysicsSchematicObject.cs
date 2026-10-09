using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using AdminToys;
using Exiled.API.Features;
using MEC;
using ProjectMER.Features;
using ProjectMER.Features.Objects;
using Slafight_Plugin_EXILED.API.Features;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Slafight_Plugin_EXILED.CustomMaps.ObjectPrefabs;

/// <summary>
/// SchematicName で宣言されたスキマティックのブロックを物理化する Prefab 基底クラス。
/// 具象クラスは SchematicName を指定し、必要なら OnPhysicsReady を拡張する。
/// </summary>
public abstract class PhysicsSchematicObject : ObjectPrefab
{
    protected abstract override string SchematicName { get; }

    private readonly List<PhysicsBlock> _physicsBlocks = [];
    private CoroutineHandle _syncCoroutine;
    private bool _physicsEnabled;
    private bool _wholeSchematicMode;
    private Vector3 _configuredPosition;
    private bool _hasConfiguredPosition;

    public override Vector3 Position
    {
        get => base.Position;
        set
        {
            // ProjectMER は他の Option の更新時にもマーカー座標を再送する。
            // 同じ設置座標なら、落下中の実体をスポーン地点へ戻さない。
            if (_physicsEnabled && _hasConfiguredPosition && value == _configuredPosition)
                return;

            _configuredPosition = value;
            _hasConfiguredPosition = true;
            base.Position = value;

            if (_physicsEnabled)
                MovePhysicsTo(value);
        }
    }

    /// <summary>この Prefab が所有する Rigidbody。破棄されたブロックは同期時に除外される。</summary>
    protected IReadOnlyList<Rigidbody> PhysicsBodies => _physicsBlocks
        .Where(block => block.Rigidbody != null)
        .Select(block => block.Rigidbody)
        .ToArray();

    /// <summary>派生クラスで初期値や挙動を override でき、Prefab Option からも設定できる。</summary>
    public virtual float Mass { get; set; } = 1f;
    /// <summary>車両との衝突で受ける力の倍率。1 が標準、0 で衝突による追加の力を受けない。</summary>
    public virtual float PowerForceMultiplier { get; set; } = 1f;
    public virtual bool UseGravity { get; set; } = true;
    public virtual bool IsKinematic { get; set; }
    public virtual bool DetachBlocks { get; set; } = true;
    /// <summary>同期コルーチンの1周期。短いほどクライアント側の追従が滑らかになる。</summary>
    public virtual float SyncInterval { get; set; } = 0.05f;
    /// <summary>
    /// 物理体が動いている間だけクライアントへ渡す NetworkMovementSmoothing。
    /// 0 だとクライアントは受信値へスナップするため、飛ぶとガタつく。
    /// </summary>
    public virtual byte MovementSmoothing { get; set; } = 0;
    public virtual Vector3 InitialVelocity { get; set; } = Vector3.zero;
    public virtual RigidbodyConstraints Constraints { get; set; } = RigidbodyConstraints.None;
    /// <summary>線形減衰。大きいほど飛翔が速く減衰し、落地点で落ち着きやすくなる。</summary>
    public virtual float LinearDamping { get; set; }
    /// <summary>角速度減衰。大きいほど回転が速く止まり、地面でのガクつきが治る。</summary>
    public virtual float AngularDamping { get; set; }

    /// <summary>
    /// 車両など外部からの強い打撃をすでに受けたかどうか。
    /// 破断後は破片が複数剛体になるため、2 回目以降の叩き込みを打ち消すために使う。
    /// </summary>
    public bool HasExternalImpact { get; private set; }

    /// <summary>物理移動するスキマティックを設置マーカーの Transform に親子付けしない。</summary>
    public override bool FollowMarkerTransform => false;

    protected override void OnCreate()
    {
        if (Schematic == null)
        {
            Log.Warn($"[{GetType().Name}] Schematic '{SchematicName}' could not be spawned.");
            return;
        }

        if (EnablePhysics(Schematic))
            OnPhysicsReady();
    }

    /// <summary>物理ブロックの生成後に派生クラスの初期化を行う。</summary>
    protected virtual void OnPhysicsReady() { }

    /// <summary>同期コルーチンの1周期ごとに呼ばれる。接触判定など物理側の反応処理に使う。</summary>
    protected virtual void OnPhysicsTick() { }

    /// <summary>
    /// 外部（リスポーン車両など）からの強い打撃。body は接触した剛体。
    /// 派生クラスは破断や追加演出をここで組み立てられる。
    /// </summary>
    public virtual void HandleExternalImpact(
        Rigidbody body,
        Vector3 direction,
        float launchSpeed,
        float upwardSpeed)
    {
        HasExternalImpact = true;
        if (body == null || body.isKinematic)
            return;

        LaunchBody(body, direction, launchSpeed, upwardSpeed);
    }

    /// <summary>剛体1個を水平方向 + 上向きの速度変化量で打ち出す。角速度は慣性で割られるため控えめに取る。</summary>
    protected static void LaunchBody(
        Rigidbody body,
        Vector3 direction,
        float launchSpeed,
        float upwardSpeed,
        float tumble = 0f)
    {
        body.WakeUp();
        body.AddForce((direction * launchSpeed + Vector3.up * upwardSpeed) * body.mass, ForceMode.Impulse);

        if (tumble > 0f)
            body.AddTorque(UnityEngine.Random.onUnitSphere * tumble, ForceMode.Impulse);
    }

    /// <summary>
    /// whole-schematic モード（複合 Collider 1 個）を破棄し、ブロック単位の Rigidbody へ移行する。
    /// 派生クラスから実行すると、設置時は一体のまま・接触時にだけ割れる演出にできる。
    /// </summary>
    protected bool DetachPhysicsBodies()
    {
        if (!_physicsEnabled || !_wholeSchematicMode || Schematic == null)
            return false;

        foreach (PhysicsBlock block in _physicsBlocks.ToList())
        {
            if (block.GameObject == null)
            {
                _physicsBlocks.Remove(block);
                continue;
            }

            // 複合本体を消すと Collider は各ブロック側へ戻り、それぞれが独立した剛体になる。
            // Destroy はフレーム末まで複合状態が残り、同一 Collider が二重所持されて
            // 破断直後の剛体が不安定になるため、ここだけ即時破棄する。
            if (block.Rigidbody != null)
                Object.DestroyImmediate(block.Rigidbody);

            if (block.GameObject.TryGetComponent(out PhysicsSchematicBodyMarker marker))
                Object.DestroyImmediate(marker);
        }

        _physicsBlocks.Clear();
        _wholeSchematicMode = false;

        // 設置時の EnablePhysics と違い、ここでは親を切らない。
        // クライアント側の階層を保ったままにすることで、親変更 RPC と
        // ローカル位置の瞬間移動を避けられる。
        foreach (GameObject block in Schematic.AttachedBlocks.Where(block => block != null))
        {
            Collider[] colliders = block.GetComponentsInChildren<Collider>(true);
            if (colliders.Length == 0)
                continue;

            Rigidbody rigidbody = block.GetComponent<Rigidbody>() ?? block.AddComponent<Rigidbody>();
            AddPhysicsBody(block, rigidbody);
        }

        if (_physicsBlocks.Count == 0)
        {
            Log.Warn($"[{GetType().Name}] Detach of '{SchematicName}' produced no physics bodies.");
            return false;
        }

        Log.Info($"[{GetType().Name}] Detached '{SchematicName}' into {_physicsBlocks.Count} physics bodies.");
        return true;
    }

    protected override void OnDestroy()
    {
        DestroyPhysicsSchematic();
        base.OnDestroy();
    }

    protected override void OnRoundRestarting()
    {
        if (_syncCoroutine.IsRunning)
            Timing.KillCoroutines(_syncCoroutine);

        _syncCoroutine = default;
        _physicsEnabled = false;
        _wholeSchematicMode = false;
        HasExternalImpact = false;
        _physicsBlocks.Clear();
        base.OnRoundRestarting();
    }

    protected override void OnTransformUpdated()
    {
        if (!_physicsEnabled)
            base.OnTransformUpdated();
    }

    public override void SyncManagedObjects()
    {
        if (!_physicsEnabled)
            base.SyncManagedObjects();
    }

    public override void ApplyOptions(Dictionary<string, string> options)
    {
        if (options == null || options.Count == 0)
            return;

        var derivedOptions = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (KeyValuePair<string, string> pair in options)
        {
            string key = NormalizeOptionKey(pair.Key);
            string value = pair.Value;

            switch (key)
            {
                case "mass":
                    if (TryParseFloat(value, out float mass) && mass > 0f)
                        Mass = mass;
                    break;
                case "powerforcemultiplier":
                    if (TryParseFloat(value, out float powerForceMultiplier) &&
                        powerForceMultiplier >= 0f && !float.IsInfinity(powerForceMultiplier))
                        PowerForceMultiplier = powerForceMultiplier;
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
                case "movementsmoothing":
                case "smoothing":
                    if (byte.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out byte smoothing))
                        MovementSmoothing = smoothing;
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
                default:
                    derivedOptions[pair.Key] = value;
                    break;
            }
        }

        base.ApplyOptions(derivedOptions);
        if (_physicsEnabled)
            ApplyRigidbodyOptions();
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

    private void DestroyPhysicsSchematic()
    {
        if (_syncCoroutine.IsRunning)
            Timing.KillCoroutines(_syncCoroutine);

        _syncCoroutine = default;
        _physicsEnabled = false;

        GameObject? schematicRoot = Schematic != null ? Schematic.gameObject : null;
        List<GameObject> detachedBlocks = _physicsBlocks
            .Select(block => block.GameObject)
            .Where(block => block != null && block != schematicRoot)
            .Distinct()
            .ToList();

        _physicsBlocks.Clear();
        _wholeSchematicMode = false;
        DestroyManagedSchematic();

        foreach (GameObject block in detachedBlocks)
        {
            if (block != null)
                Object.Destroy(block);
        }
    }

    private bool EnablePhysics(SchematicObject schematic)
    {
        _physicsBlocks.Clear();
        // 動くブロックを静的プリミティブ最適化のクライアント専用表示から戻す。
        PrimitiveOptimizationManager.PromoteSchematic(schematic);

        _wholeSchematicMode = !DetachBlocks;
        if (_wholeSchematicMode)
        {
            Collider[] colliders = schematic.GetComponentsInChildren<Collider>(true);
            if (colliders.Length == 0)
            {
                Log.Warn($"[{GetType().Name}] Schematic '{SchematicName}' has no colliders for whole-object physics.");
                return false;
            }

            // 子に Rigidbody が残ると複合 Collider が分断されるため、ルートだけを物理本体にする。
            foreach (Rigidbody childBody in schematic.GetComponentsInChildren<Rigidbody>(true))
            {
                if (childBody == null || childBody.gameObject == schematic.gameObject)
                    continue;

                childBody.isKinematic = true;
                childBody.detectCollisions = false;
                Object.Destroy(childBody);
            }

            Rigidbody rootBody = schematic.GetComponent<Rigidbody>() ?? schematic.gameObject.AddComponent<Rigidbody>();
            AddPhysicsBody(schematic.gameObject, rootBody);
        }
        else
        {
            foreach (GameObject block in schematic.AttachedBlocks.Where(block => block != null))
            {
                Collider[] colliders = block.GetComponentsInChildren<Collider>(true);
                if (colliders.Length == 0)
                    continue;

                block.transform.SetParent(null, true);
                Rigidbody rigidbody = block.GetComponent<Rigidbody>() ?? block.AddComponent<Rigidbody>();
                AddPhysicsBody(block, rigidbody);
            }
        }

        _physicsEnabled = _physicsBlocks.Count > 0;
        if (_physicsEnabled)
            _syncCoroutine = Timing.RunCoroutine(SyncPhysicsBlocks());

        Log.Info($"[{GetType().Name}] Enabled physics for {_physicsBlocks.Count} bodies in '{SchematicName}'.");
        return _physicsEnabled;
    }

    private void AddPhysicsBody(GameObject bodyObject, Rigidbody rigidbody)
    {
        ApplyRigidbodyOptions(rigidbody);
        PhysicsSchematicBodyMarker marker = bodyObject.GetComponent<PhysicsSchematicBodyMarker>() ??
                                            bodyObject.AddComponent<PhysicsSchematicBodyMarker>();
        marker.Body = rigidbody;
        marker.Owner = this;

        AdminToyBase[] toys = bodyObject.GetComponentsInChildren<AdminToyBase>(true);
        foreach (AdminToyBase toy in toys)
        {
            toy.NetworkIsStatic = false;
            toy.NetworkMovementSmoothing = 0;
            SyncToy(toy);
        }

        if (InitialVelocity != Vector3.zero)
            rigidbody.linearVelocity = Rotation * InitialVelocity;

        rigidbody.WakeUp();
        _physicsBlocks.Add(new PhysicsBlock(bodyObject, rigidbody, toys));
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
        rigidbody.linearDamping = LinearDamping;
        rigidbody.angularDamping = AngularDamping;
        rigidbody.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        rigidbody.interpolation = RigidbodyInterpolation.Interpolate;
    }

    private void MovePhysicsTo(Vector3 target)
    {
        Vector3 offset = Vector3.zero;
        if (!_wholeSchematicMode)
        {
            Rigidbody[] bodies = PhysicsBodies.ToArray();
            if (bodies.Length == 0)
                return;

            Vector3 center = Vector3.zero;
            foreach (Rigidbody body in bodies)
                center += body.position;
            offset = target - center / bodies.Length;
        }

        foreach (PhysicsBlock block in _physicsBlocks)
        {
            Rigidbody body = block.Rigidbody;
            if (body == null)
                continue;

            body.position = _wholeSchematicMode ? target : body.position + offset;
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
            body.WakeUp();

            foreach (AdminToyBase toy in block.Toys)
                SyncToy(toy);
        }
    }

    private IEnumerator<float> SyncPhysicsBlocks()
    {
        while (_physicsBlocks.Count > 0)
        {
            if (_wholeSchematicMode && Schematic != null)
            {
                base.Position = Schematic.Position;
                base.Rotation = Schematic.Rotation;
            }
            else if (!_wholeSchematicMode)
            {
                Rigidbody[] bodies = PhysicsBodies.ToArray();
                if (bodies.Length > 0)
                {
                    Vector3 center = Vector3.zero;
                    foreach (Rigidbody body in bodies)
                        center += body.position;
                    base.Position = center / bodies.Length;
                }
            }

            bool moving = IsMoving();
            foreach (PhysicsBlock block in _physicsBlocks.ToList())
            {
                if (block.GameObject == null)
                {
                    _physicsBlocks.Remove(block);
                    continue;
                }

                ApplyToySmoothing(block, moving);
                foreach (AdminToyBase toy in block.Toys)
                    SyncToy(toy);
            }

            OnPhysicsTick();
            yield return Timing.WaitForSeconds(SyncInterval);
        }
    }

    /// <summary>物理体が1つでも動いている間だけ補間平滑値を切り替え、静止時は位置をスナップで合わせる。</summary>
    private void ApplyToySmoothing(PhysicsBlock block, bool moving)
    {
        byte smoothing = moving ? MovementSmoothing : (byte)0;
        if (block.AppliedSmoothing == smoothing)
            return;

        block.AppliedSmoothing = smoothing;
        foreach (AdminToyBase toy in block.Toys)
        {
            if (toy != null)
                toy.NetworkMovementSmoothing = smoothing;
        }
    }

    private bool IsMoving()
    {
        const float linearThreshold = 0.05f;
        const float angularThreshold = 0.05f;

        foreach (PhysicsBlock block in _physicsBlocks)
        {
            Rigidbody body = block.Rigidbody;
            if (body == null || body.isKinematic)
                continue;

            if (body.linearVelocity.sqrMagnitude > linearThreshold * linearThreshold ||
                body.angularVelocity.sqrMagnitude > angularThreshold * angularThreshold)
            {
                return true;
            }
        }

        return false;
    }

    private static void SyncToy(AdminToyBase toy)
    {
        if (toy == null)
            return;

        // AdminToyBase の Position / Rotation は親基準のローカル値。ワールド位置で書くと
        // スキマティック配下のブロックはクライアントで親分だけ飛んでしまう。
        Transform transform = toy.transform;
        toy.NetworkPosition = transform.localPosition;
        toy.NetworkRotation = transform.localRotation;
        toy.NetworkScale = transform.localScale;
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

    private sealed class PhysicsBlock
    {
        public PhysicsBlock(GameObject gameObject, Rigidbody rigidbody, AdminToyBase[] toys)
        {
            GameObject = gameObject;
            Rigidbody = rigidbody;
            Toys = toys;
            AppliedSmoothing = 0;
        }

        public GameObject GameObject { get; }
        public Rigidbody Rigidbody { get; }
        public AdminToyBase[] Toys { get; }

        /// <summary>Toy へ最後に渡した NetworkMovementSmoothing。再送を避けて差分だけ更新する。</summary>
        public byte AppliedSmoothing { get; set; }
    }
}
