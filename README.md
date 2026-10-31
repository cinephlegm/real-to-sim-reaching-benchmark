# Real-to-Sim Learning for Dexterous Arm and Hand-Manipulation Tasks

Curated research repository for the BSc (Hons) Digital Games Development dissertation:

**Real-To-Sim Learning for Dexterous Arm and Hand-Manipulation Tasks**

The research evaluates a real-to-simulation pipeline for training a physics-based virtual KUKA iiwa7 seven-degree-of-freedom arm and SCHUNK SVH five-finger hand from captured human motion.

## Study design

Two learning conditions were compared:

- **Condition A: BC-only**  
  Behavioural Cloning from the recorded demonstrations, with no extrinsic task reward contributing to the learning signal.

- **Condition B: BC-assisted PPO refinement**  
  The same demonstration set is used as an early Behavioural Cloning prior before Proximal Policy Optimisation refinement becomes the effective learning signal.

Human hand intent was captured through a Kinect-based vision pipeline and converted into low-dimensional per-finger robot control values during demonstration recording.

## Final implementation

The submitted dissertation defines:

- **27 observations**
  - 7 normalised arm articulation target values
  - 6 hand-state values
  - cube position relative to the palm
  - target position relative to the cube
  - validated-grasp flag
  - hold/attachment flag
  - cube linear velocity
  - cube angular velocity
- **13 continuous actions**
  - 7 arm joint-delta actions
  - 6 hand actions: thumb, index, middle, ring, pinky and spread
- **60 recorded demonstration episodes**
- fixed task layout for the primary A/B comparison
- 500-decision-step episode timeout
- inference-time stress testing for spatial and observation perturbations

### Final training settings

| Parameter | Value |
| --- | --- |
| Learning rate | 3e-4 |
| Batch size | 1024 |
| Buffer size | 10240 |
| PPO clip epsilon | 0.2 |
| Discount factor gamma | 0.99 |
| Time horizon | 256 |
| Network | 2 layers x 256 units |
| Observation normalisation | Enabled |
| BC dataset | 60 episodes |
| Condition B BC schedule | First 500k steps, then decays to zero |

## Final reported results

The submitted dissertation reports:

- BC-only: **0%** target-region success at the final reported Condition A checkpoint, approximately 1.206M logged steps.
- BC-assisted PPO: approximately **95%** target-region success under the fixed training layout.
- Fixed-layout inference baseline: **100%** success.
- +/-3 cm cube-spawn offset: **69%** success.
- +/-6 cm cube-spawn offset: **19%** success.
- +/-10 cm cube-spawn offset: **5%** success.
- +/-15 cm cube-spawn offset: **9%** success.
- +/-6 cm offset plus low observation noise: **23%** success.
- Observation-noise-only conditions: **100%** success in both tested cases.

The success metric measures whether the cube enters the target placement radius. It does **not** require a stable autonomous release, so the demonstrated behaviour is better described as grasp-and-transport-to-target rather than full pick-place-release.

The dissertation completed one training run per condition. The A/B findings are therefore descriptive rather than statistically generalisable. The 800 inference-time stress-test episodes characterise the robustness of the final Condition B policy, not the expected performance across independently trained seeds.

## Technical environment

- Unity 2022.3.62f3
- Unity ML-Agents package 2.3.0-exp.3
- Unity Robotics URDF Importer 0.5.2
- KUKA iiwa7 arm
- SCHUNK SVH hand
- Unity ArticulationBody physics
- Custom ML-Agent, metrics, stress-test and episode-management tooling

## Public-repository scope

The original private research archive is roughly 770 MB and contains raw demonstrations, training outputs, ROS/URDF source material, thesis build files, debug logs and intermediate experiments.

This repository is intentionally a curated publication artifact. It will contain:

- original custom source code required to reproduce the experiment;
- the exact final methodology and trainer configuration;
- compact result datasets and plots;
- reproduction instructions;
- the submitted dissertation PDF;
- attribution and licensing information for robot-description dependencies;
- selected demo media.

Large transient logs, IDE state, generated Unity data and unrelated thesis-build intermediates will remain outside the public repository.

## Reproducibility

The migration is being completed from the private research archive. Before public release, the repository will document exact robot-model provenance, demonstration-data handling and the commands required to reproduce training and evaluation.

## License

No repository-wide license has been applied yet. Custom code, the dissertation text/data and third-party robot assets will be licensed or attributed independently before public release.
