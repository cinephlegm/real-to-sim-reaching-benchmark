using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Policies;
using Unity.MLAgents.Sensors;
using UnityEngine;
using UnityEngine.InputSystem;

public class DexterousPickPlaceAgent : Agent
{
    const int ArmActionCount = 7;
    const int LegacyActionCount = 10;
    const int PerFingerActionCount = 13;

    [Header("Robot")]
    public ArticulationBody[] armJoints = new ArticulationBody[7];
    public Transform palmAttachPoint;
    public SvhSynergyHandDriver handDriver;

    [Header("Episode control")]
    public bool resetOnEpisodeBegin = true;
    public bool snapArmHomeOnEpisodeBegin = false;
    public IiwaArmPosePresetController armPreset;

    [Header("Agent action safety")]
    public bool applyArmActions = true;
    public bool applyHandActions = true;
    public bool holdHandWhenActionsAreNeutral = true;
    public bool logActionSpecWarning = true;
    [Tooltip("Block OnActionReceived from driving joints when BehaviorType=InferenceOnly but no ONNX model is loaded. " +
             "Prevents random actions from exploding the arm/hand. Set Behavior Type to Heuristic Only for demo recording.")]
    public bool blockActionsIfInferenceWithNoModel = true;
    [Tooltip("Disable to suppress reward calculation and episode timeout resets. " +
             "Set FALSE during demo recording so the 20s timeout does not auto-reset the cube mid-demo.")]
    public bool enableEpisodeTermination = true;
    [Tooltip("Exit Play Mode after each episode ends. Enable ONLY during demo recording. Must be OFF during BC/PPO training.")]
    public bool exitPlayModeOnEpisodeEnd = false;

    [Header("MediaPipe UDP")]
    public UdpSynergyReceiver udpReceiver;

    [Header("Task")]
    public GraspValidator validator;
    public HybridHoldController hold;
    public EpisodeResetter resetter;
    public MetricsReporter metrics;
    public DemoStatsLogger demoStats;
    public Rigidbody cube;
    public Transform placeMarker;

    [Header("Stress Testing (optional)")]
    [Tooltip("Assign StressTestController to enable robustness evaluation runs. Leave null for normal training.")]
    public StressTestController stressTest;

    [Header("Condition Variant")]
    [Tooltip("OFF = Condition A / B (normal grasp, minHandClose ~0.80).  ON = Condition B2 (loose grasp threshold, enables throw-to-goal behaviour).")]
    public bool throwCondition = false;
    [Tooltip("minHandClose used for Condition A / B (normal).")]
    public float graspThresholdNormal = 0.80f;
    [Tooltip("minHandClose used for Condition B2 (throw). Should match the loose value used during B2 training, e.g. 0.20.")]
    public float graspThresholdThrow = 0.20f;

    [Header("Action Magnitudes")]
    public float armDeltaPerStepDeg = 2f;
    public float handDeltaPerStep = 0.015f;

    [Header("Reward Thresholds")]
    public float placeRadius = 0.06f;
    public float liftHeightRewardThreshold = 0.04f;
    public float timeoutSeconds = 20f;
    public bool requireReleaseForSuccess = false;
    public float releaseCloseThreshold = 0.20f;
    public float maxPlaceSpeed = 0.10f;
    public float approachRewardScale = 0.001f;
    public float timePenaltyPerStep = 0.0005f;
    public float holdRewardPerStep = 0.001f;       // per-step reward for maintaining grip after validated grasp
    public float gripLossPenalty = 1.0f;           // penalty when grasp breaks after validation
    public float transportProgressScale = 0.005f;  // reward per metre cube moves toward place marker (after lift)
    public float dropPenalty = 1.5f;               // penalty when cube hits floor
    public float cubeHeightRewardPerStep = 0.002f; // per-step reward for keeping cube elevated during transport
    public float nearPlaceBonusPerStep = 0.003f;   // per-step bonus when holding cube within 2x placeRadius of destination
    public float lazyEpisodePenalty = 0.5f;        // extra penalty on timeout if agent never grasped at all

    [Header("Heuristic Keys")]
    public Key joint1Key = Key.Digit1;
    public Key joint2Key = Key.Digit2;
    public Key joint3Key = Key.Digit3;
    public Key joint4Key = Key.Digit4;
    public Key joint5Key = Key.Digit5;
    public Key joint6Key = Key.Digit6;
    public Key joint7Key = Key.Digit7;
    public Key armNegativeKey = Key.Q;
    public Key armPositiveKey = Key.E;
    public Key handCloseMoreKey = Key.R;
    public Key handCloseLessKey = Key.F;
    public Key thumbOpposeMoreKey = Key.T;
    public Key thumbOpposeLessKey = Key.G;
    public Key fingerSpreadMoreKey = Key.Y;
    public Key fingerSpreadLessKey = Key.H;

    int _selectedJoint;
    readonly float[] _armTargetsDeg = new float[7];

    int _dbgEpisodeCount;
    int _dbgObsCount;
    int _dbgActionCount;

    float _hThumb;
    float _hIndex;
    float _hMiddle;
    float _hRing;
    float _hPinky;
    float _hSpread;

    float _timer;
    Vector3 _startCubePos;
    bool _episodeValidatedGrasp;
    bool _episodeLifted;
    bool _wasHoldingLastStep;
    bool _loggedActionSpec;
    bool _droppedFloor;
    bool _graspLost;
    bool _milestone30cm;
    bool _milestone15cm;
    bool _milestone5cm;
    float _minDistToPlace;
    float _lastDistToPlace;
    float _timeToGrasp;
    float _timeToLift;

    public override void Initialize()
    {
        Debug.Log($"[Agent:{name}] Initialize() — armJoints:{armJoints?.Length} handDriver:{handDriver != null} resetter:{resetter != null} cube:{cube != null} validator:{validator != null} armPreset:{armPreset != null}");
        SyncArmTargetsFromDrives();
        SyncHandStateFromDriver();
        Debug.Log($"[Agent:{name}] Initialize() done — arm targets: [{string.Join(", ", System.Array.ConvertAll(_armTargetsDeg, d => d.ToString("F1")))}]");
    }

    public override void OnEpisodeBegin()
    {
        _dbgEpisodeCount++;
        Debug.Log($"[Agent:{name}] OnEpisodeBegin() START — episode #{_dbgEpisodeCount}  resetOnEpisodeBegin:{resetOnEpisodeBegin}  snapArmHome:{snapArmHomeOnEpisodeBegin}");

        if (resetOnEpisodeBegin && resetter != null)
        {
            Debug.Log($"[Agent:{name}] OnEpisodeBegin() calling resetter.ResetEpisode()...");
            resetter.ResetEpisode();
            Debug.Log($"[Agent:{name}] OnEpisodeBegin() resetter.ResetEpisode() returned.");
        }
        else
            Debug.Log($"[Agent:{name}] OnEpisodeBegin() skipping resetter (resetOnEpisodeBegin={resetOnEpisodeBegin} resetter={(resetter == null ? "NULL" : "ok")})");

        // Apply grasp threshold for the selected condition variant
        if (validator != null)
            validator.minHandClose = throwCondition ? graspThresholdThrow : graspThresholdNormal;

        if (snapArmHomeOnEpisodeBegin && armPreset != null)
        {
            Debug.Log($"[Agent:{name}] OnEpisodeBegin() calling SnapToHomeImmediate()...");
            armPreset.SnapToHomeImmediate();
            Debug.Log($"[Agent:{name}] OnEpisodeBegin() SnapToHomeImmediate() returned.");
        }

        Debug.Log($"[Agent:{name}] OnEpisodeBegin() calling SyncArmTargetsFromDrives()...");
        SyncArmTargetsFromDrives();
        SyncHandStateFromDriver();

        _timer = 0f;
        _droppedFloor = false;
        _graspLost = false;
        _milestone30cm = false;
        _milestone15cm = false;
        _milestone5cm = false;
        _lastDistToPlace = float.MaxValue;
        _episodeValidatedGrasp = false;
        _episodeLifted = false;
        _wasHoldingLastStep = IsCurrentlyHolding();
        _minDistToPlace = float.MaxValue;
        _timeToGrasp = -1f;
        _timeToLift = -1f;

        if (cube != null)
            _startCubePos = cube.position;

        if (demoStats != null)
            demoStats.ResetAccumulators();

        Debug.Log($"[Agent:{name}] OnEpisodeBegin() DONE — cubePos:{_startCubePos}  arm[0]:{_armTargetsDeg[0]:F1}  holding:{_wasHoldingLastStep}");
    }

    public override void CollectObservations(VectorSensor sensor)
    {
        _dbgObsCount++;
        if (_dbgObsCount <= 3 || _dbgObsCount % 500 == 0)
            Debug.Log($"[Agent:{name}] CollectObservations() call #{_dbgObsCount}  t={_timer:F2}  palmAttach:{palmAttachPoint != null}  cube:{cube != null}");

        for (int i = 0; i < 7; i++)
            sensor.AddObservation(Mathf.Clamp(stressTest != null ? stressTest.Noise(_armTargetsDeg[i] / 180f) : _armTargetsDeg[i] / 180f, -1f, 1f));

        sensor.AddObservation(stressTest != null ? stressTest.Noise(_hThumb) : _hThumb);
        sensor.AddObservation(stressTest != null ? stressTest.Noise(_hIndex) : _hIndex);
        sensor.AddObservation(stressTest != null ? stressTest.Noise(_hMiddle) : _hMiddle);
        sensor.AddObservation(stressTest != null ? stressTest.Noise(_hRing) : _hRing);
        sensor.AddObservation(stressTest != null ? stressTest.Noise(_hPinky) : _hPinky);
        sensor.AddObservation(stressTest != null ? stressTest.Noise(_hSpread) : _hSpread);

        if (palmAttachPoint != null && cube != null)
            sensor.AddObservation(palmAttachPoint.InverseTransformPoint(cube.worldCenterOfMass));
        else
            sensor.AddObservation(Vector3.zero);

        if (cube != null && placeMarker != null)
            sensor.AddObservation(cube.transform.InverseTransformPoint(placeMarker.position));
        else
            sensor.AddObservation(Vector3.zero);

        sensor.AddObservation(validator != null && validator.IsValidated ? 1f : 0f);
        sensor.AddObservation(hold != null && hold.attached ? 1f : 0f);
        sensor.AddObservation(cube != null ? cube.velocity : Vector3.zero);
        sensor.AddObservation(cube != null ? cube.angularVelocity : Vector3.zero);
    }

    public override void OnActionReceived(ActionBuffers actions)
    {
        _dbgActionCount++;

        if (blockActionsIfInferenceWithNoModel)
        {
            var bp = GetComponent<BehaviorParameters>();
            if (bp != null && bp.BehaviorType == BehaviorType.InferenceOnly && bp.Model == null)
                return;
        }

        var a = actions.ContinuousActions;
        int len = a.Length;

        LogActionSpecIfNeeded(len);

        _timer += Time.fixedDeltaTime;

        if (applyArmActions)
            ApplyArmActionSegment(a, len);

        if (applyHandActions)
            ApplyHandActionSegment(a, len);

        if (demoStats != null)
        {
            float wr = udpReceiver != null ? udpReceiver.WristRoll : 0f;
            demoStats.RecordStep(_hThumb, _hIndex, _hMiddle, _hRing, _hPinky, _hSpread, wr);
        }

        EvaluateRewardsAndTermination();
    }

    void ApplyArmActionSegment(ActionSegment<float> a, int len)
    {
        int count = Mathf.Min(ArmActionCount, len);

        for (int i = 0; i < count; i++)
        {
            float delta = Mathf.Clamp(a[i], -1f, 1f) * armDeltaPerStepDeg;
            _armTargetsDeg[i] = ClampJointTarget(i, _armTargetsDeg[i] + delta);
        }

        ApplyArmTargets();
    }

    void ApplyHandActionSegment(ActionSegment<float> a, int len)
    {
        if (handDriver == null)
            return;

        if (len >= PerFingerActionCount)
        {
            if (holdHandWhenActionsAreNeutral && IsNeutralActionRange(a, 7, 13))
                return;

            _hThumb = RemapAction01(a[7]);
            _hIndex = RemapAction01(a[8]);
            _hMiddle = RemapAction01(a[9]);
            _hRing = RemapAction01(a[10]);
            _hPinky = RemapAction01(a[11]);
            _hSpread = RemapAction01(a[12]);

            handDriver.SetFingers(_hThumb, _hIndex, _hMiddle, _hRing, _hPinky, _hSpread);
            return;
        }

        if (len >= LegacyActionCount)
        {
            if (holdHandWhenActionsAreNeutral && IsNeutralActionRange(a, 7, 10))
                return;

            float close = RemapAction01(a[7]);
            float oppose = RemapAction01(a[8]);
            float spread = RemapAction01(a[9]);

            _hThumb = oppose;
            _hIndex = close;
            _hMiddle = close;
            _hRing = close;
            _hPinky = close;
            _hSpread = spread;

            handDriver.SetSynergies(close, oppose, spread);
        }
    }

    public override void Heuristic(in ActionBuffers actionsOut)
    {
        var a = actionsOut.ContinuousActions;
        int len = a.Length;
        var kb = Keyboard.current;

        for (int i = 0; i < len; i++)
            a[i] = 0f;

        if (kb != null)
        {
            if (kb[joint1Key].wasPressedThisFrame) _selectedJoint = 0;
            if (kb[joint2Key].wasPressedThisFrame) _selectedJoint = 1;
            if (kb[joint3Key].wasPressedThisFrame) _selectedJoint = 2;
            if (kb[joint4Key].wasPressedThisFrame) _selectedJoint = 3;
            if (kb[joint5Key].wasPressedThisFrame) _selectedJoint = 4;
            if (kb[joint6Key].wasPressedThisFrame) _selectedJoint = 5;
            if (kb[joint7Key].wasPressedThisFrame) _selectedJoint = 6;

            if (armPreset != null && armPreset.IsMovingToPreset && armPreset.ActivePresetDeg != null)
            {
                int armLen = Mathf.Min(ArmActionCount, len);
                for (int i = 0; i < armLen; i++)
                {
                    float delta = (armPreset.ActivePresetDeg[i] - _armTargetsDeg[i]) / armDeltaPerStepDeg;
                    a[i] = Mathf.Clamp(delta, -1f, 1f);
                }
            }
            else if (_selectedJoint >= 0 && _selectedJoint < len)
            {
                if (kb[armNegativeKey].isPressed) a[_selectedJoint] = -1f;
                else if (kb[armPositiveKey].isPressed) a[_selectedJoint] = 1f;
            }

            if (kb[handCloseMoreKey].isPressed)
            {
                _hIndex = Mathf.Clamp01(_hIndex + handDeltaPerStep);
                _hMiddle = Mathf.Clamp01(_hMiddle + handDeltaPerStep);
                _hRing = Mathf.Clamp01(_hRing + handDeltaPerStep);
                _hPinky = Mathf.Clamp01(_hPinky + handDeltaPerStep);
            }
            else if (kb[handCloseLessKey].isPressed)
            {
                _hIndex = Mathf.Clamp01(_hIndex - handDeltaPerStep);
                _hMiddle = Mathf.Clamp01(_hMiddle - handDeltaPerStep);
                _hRing = Mathf.Clamp01(_hRing - handDeltaPerStep);
                _hPinky = Mathf.Clamp01(_hPinky - handDeltaPerStep);
            }

            if (kb[thumbOpposeMoreKey].isPressed) _hThumb = Mathf.Clamp01(_hThumb + handDeltaPerStep);
            else if (kb[thumbOpposeLessKey].isPressed) _hThumb = Mathf.Clamp01(_hThumb - handDeltaPerStep);

            if (kb[fingerSpreadMoreKey].isPressed) _hSpread = Mathf.Clamp01(_hSpread + handDeltaPerStep);
            else if (kb[fingerSpreadLessKey].isPressed) _hSpread = Mathf.Clamp01(_hSpread - handDeltaPerStep);
        }

        if (udpReceiver != null && udpReceiver.HasFreshData)
        {
            _hThumb = udpReceiver.ThumbFlex;
            _hIndex = udpReceiver.IndexFlex;
            _hMiddle = udpReceiver.MiddleFlex;
            _hRing = udpReceiver.RingFlex;
            _hPinky = udpReceiver.PinkyFlex;
            _hSpread = udpReceiver.FingerSpread;
        }

        WriteHandHeuristicActions(a, len);
    }

    void WriteHandHeuristicActions(ActionSegment<float> a, int len)
    {
        if (len >= PerFingerActionCount)
        {
            a[7] = _hThumb * 2f - 1f;
            a[8] = _hIndex * 2f - 1f;
            a[9] = _hMiddle * 2f - 1f;
            a[10] = _hRing * 2f - 1f;
            a[11] = _hPinky * 2f - 1f;
            a[12] = _hSpread * 2f - 1f;
            return;
        }

        if (len >= LegacyActionCount)
        {
            float close = (_hIndex + _hMiddle + _hRing + _hPinky) * 0.25f;
            a[7] = close * 2f - 1f;
            a[8] = _hThumb * 2f - 1f;
            a[9] = _hSpread * 2f - 1f;
        }
    }

    void EvaluateRewardsAndTermination()
    {
        bool currentlyHolding = IsCurrentlyHolding();

        // One-time validated grasp reward
        if (!_episodeValidatedGrasp && validator != null && validator.IsValidated)
        {
            _episodeValidatedGrasp = true;
            _timeToGrasp = _timer;
            AddReward(0.5f);
        }

        // Grip-loss: penalty when grasp breaks after validation, but continue episode so PPO isn't afraid to move
        // Exception: no penalty if cube is already at the target — that's the intended release
        if (!_graspLost && _episodeValidatedGrasp && _wasHoldingLastStep && !currentlyHolding)
        {
            bool cubeNearTarget = placeMarker != null && cube != null
                && Vector3.Distance(cube.position, placeMarker.position) <= placeRadius;
            if (!cubeNearTarget)
            {
                _graspLost = true;
                AddReward(-gripLossPenalty);
            }
            // Do NOT end episode — let PPO attempt recovery or time out naturally
        }

        // Per-step hold reward: ONLY after lift — prevents farming reward by just grabbing and standing still
        if (_episodeLifted && currentlyHolding)
            AddReward(holdRewardPerStep);

        // One-time lift reward
        if (_episodeValidatedGrasp && !_episodeLifted && cube != null)
        {
            if (cube.position.y - _startCubePos.y >= liftHeightRewardThreshold)
            {
                _episodeLifted = true;
                _timeToLift = _timer;
                AddReward(1.0f);
            }
        }

        // Approach reward: continuous gradient toward cube before grasp only
        if (!_episodeValidatedGrasp && palmAttachPoint != null && cube != null)
        {
            float dist = Vector3.Distance(palmAttachPoint.position, cube.worldCenterOfMass);
            AddReward(-approachRewardScale * dist);
        }

        // Transport progress: dense shaping toward place marker once cube is lifted
        if (_episodeLifted && placeMarker != null && cube != null)
        {
            float distToPlace = Vector3.Distance(cube.position, placeMarker.position);
            if (_lastDistToPlace < float.MaxValue)
                AddReward(transportProgressScale * (_lastDistToPlace - distToPlace));
            _lastDistToPlace = distToPlace;

            // Cube height reward: keep it elevated while transporting (don't drag)
            if (cube.position.y - _startCubePos.y >= liftHeightRewardThreshold)
                AddReward(cubeHeightRewardPerStep);

            // Near-place bonus: reward hovering over destination while holding
            if (currentlyHolding && distToPlace <= placeRadius * 2f)
                AddReward(nearPlaceBonusPerStep);
        }

        AddReward(-timePenaltyPerStep);

        // Placement success
        if (placeMarker != null && cube != null)
        {
            float distToPlace = Vector3.Distance(cube.position, placeMarker.position);
            if (distToPlace < _minDistToPlace) _minDistToPlace = distToPlace;

            bool cubeAtTarget = distToPlace <= placeRadius;

            // Release shaping: reward opening hand when cube is close to target.
            if (cubeAtTarget)
            {
                AddReward(3.0f);
                EndEpisodeWithMetrics(success: true, timedOut: false);
                return;
            }
        }

        // Fallback drop detection: cube returned near START position (not the place marker).
        // Only fires if cube is close to where it began, so intentional placement elsewhere is never confused.
        if (!_droppedFloor && _episodeValidatedGrasp && cube != null
            && cube.position.y < _startCubePos.y + 0.03f
            && Vector3.Distance(new Vector3(cube.position.x, 0, cube.position.z),
                                new Vector3(_startCubePos.x, 0, _startCubePos.z)) < 0.15f
            && _timer > _timeToGrasp + 1.0f)
        {
            _droppedFloor = true;
            AddReward(-dropPenalty);
            EndEpisodeWithMetrics(success: false, timedOut: false);
            return;
        }

        if (enableEpisodeTermination && _timer >= timeoutSeconds)
        {
            // Extra penalty if agent timed out without ever attempting a grasp
            if (!_episodeValidatedGrasp)
                AddReward(-lazyEpisodePenalty);
            EndEpisodeWithMetrics(success: false, timedOut: true);
            return;
        }

        _wasHoldingLastStep = currentlyHolding;
    }

    /// <summary>Called by CubeFloorTrigger when the cube hits the floor.</summary>
    public void NotifyDroppedToFloor()
    {
        if (_droppedFloor) return;
        _droppedFloor = true;
        AddReward(-dropPenalty);
        EndEpisodeWithMetrics(success: false, timedOut: false);
    }

    void EndEpisodeWithMetrics(bool success, bool timedOut)
    {
        if (metrics != null)
        {
            metrics.LogEpisodeResult(success, _timer, timedOut, _episodeValidatedGrasp);
            if (_droppedFloor)
                metrics.LogDrop();
        }
        if (demoStats != null)
            demoStats.LogEpisode(success, _timer, _episodeValidatedGrasp, _episodeLifted, _droppedFloor,
                                 _minDistToPlace < float.MaxValue ? _minDistToPlace : -1f,
                                 _timeToGrasp, _timeToLift);
        if (stressTest != null)
            stressTest.RecordEpisode(success, _timer, timedOut,
                                     _minDistToPlace < float.MaxValue ? _minDistToPlace : -1f);
        EndEpisode();
#if UNITY_EDITOR
        if (exitPlayModeOnEpisodeEnd)
            UnityEditor.EditorApplication.isPlaying = false;
#endif
    }

    void SyncArmTargetsFromDrives()
    {
        int nullCount = 0;
        for (int i = 0; i < 7; i++)
        {
            if (armJoints != null && i < armJoints.Length && armJoints[i] != null)
                _armTargetsDeg[i] = armJoints[i].xDrive.target;
            else
                nullCount++;
        }
        if (nullCount > 0)
            Debug.LogError($"[Agent:{name}] SyncArmTargetsFromDrives() — {nullCount} NULL arm joints! Check prefab wiring.");
    }

    void SyncHandStateFromDriver()
    {
        if (handDriver == null)
            return;

        _hThumb = handDriver.thumbOppose;
        _hIndex = handDriver.handClose;
        _hMiddle = handDriver.handClose;
        _hRing = handDriver.handClose;
        _hPinky = handDriver.handClose;
        _hSpread = handDriver.fingerSpread;
    }

    void ApplyArmTargets()
    {
        for (int i = 0; i < 7; i++)
        {
            if (armJoints == null || i >= armJoints.Length || armJoints[i] == null) continue;
            var drive = armJoints[i].xDrive;
            drive.target = ClampJointTarget(i, _armTargetsDeg[i]);
            armJoints[i].xDrive = drive;
        }
    }

    float ClampJointTarget(int idx, float targetDeg)
    {
        if (armJoints == null || idx >= armJoints.Length || armJoints[idx] == null) return targetDeg;
        var drive = armJoints[idx].xDrive;
        return Mathf.Clamp(targetDeg, drive.lowerLimit, drive.upperLimit);
    }

    bool IsCurrentlyHolding()
    {
        bool validated = validator != null && validator.IsValidated;
        bool attached = hold != null && hold.attached;
        return validated || attached;
    }

    float RemapAction01(float value)
    {
        return Mathf.Clamp01((Mathf.Clamp(value, -1f, 1f) + 1f) * 0.5f);
    }

    bool IsNeutralActionRange(ActionSegment<float> a, int startInclusive, int endExclusive)
    {
        for (int i = startInclusive; i < endExclusive; i++)
        {
            if (Mathf.Abs(a[i]) > 0.0001f)
                return false;
        }
        return true;
    }

    void LogActionSpecIfNeeded(int len)
    {
        if (!logActionSpecWarning || _loggedActionSpec)
            return;

        _loggedActionSpec = true;

        if (len == PerFingerActionCount)
        {
            Debug.Log("[DexterousPickPlaceAgent] Action spec OK: 13 continuous actions, per-finger mode.");
            return;
        }

        if (len == LegacyActionCount)
        {
            Debug.LogWarning("[DexterousPickPlaceAgent] Action spec is 10 continuous actions. Running legacy 3-synergy compatibility mode. Update BehaviorParameters to 13 for current per-finger method.");
            return;
        }

        Debug.LogError($"[DexterousPickPlaceAgent] Unexpected continuous action count: {len}. Expected 13 current or 10 legacy. Agent actions will be partially ignored to prevent physics explosions.");
    }

    void OnGUI()
    {
        const float X = 10, Y = 10, W = 560;

        bool udpActive = udpReceiver != null && udpReceiver.HasFreshData;
        int pkts = udpReceiver != null ? udpReceiver.PacketsReceived : 0;
        float wr = udpReceiver != null ? udpReceiver.WristRoll : 0f;

        string udpStr = udpActive ? $"UDP ✓ rx:{pkts}" : "KB";
        string presetMode = armPreset != null && armPreset.heuristicDrivenMode ? "BC" : "Direct";
        string presetMove = armPreset != null && armPreset.IsMovingToPreset ? " >>Moving<<" : "";
        string graspValid = validator != null
            ? (validator.IsValidated
                ? "GRASP ✓"
                : $"✗ {validator.StableTimer:F2}/{validator.stableTimeRequired:F2}  {validator.LastFailureReason}")
            : "no validator";

        string line1 = $"t={_timer:F1}s  {udpStr}  Presets[{presetMode}]{presetMove}  Jt:{_selectedJoint + 1}" +
                       $"  G:{(_episodeValidatedGrasp ? "✓" : "✗")} L:{(_episodeLifted ? "✓" : "✗")} DF:{(_droppedFloor ? "✓" : "✗")}";
        string line2 = $"T={_hThumb:F2}  I={_hIndex:F2}  M={_hMiddle:F2}  R={_hRing:F2}  P={_hPinky:F2}  Sp={_hSpread:F2}  Wr={wr:F2}";
        string line3 = $"Validator: {graspValid}";
        string line4 = "Keys: 1-7=Jt  Q/E=arm  R/F=close  T/G=thumb  Y/H=spread  V=validate  Bksp=reset";

        GUILayout.BeginArea(new Rect(X, Y, W, 84), GUI.skin.box);
        GUILayout.Label(line1);
        GUILayout.Label(line2);
        GUILayout.Label(line3);
        GUILayout.Label(line4);
        GUILayout.EndArea();
    }
}
