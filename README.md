# Real-to-Sim Learning for Dexterous Arm and Hand-Manipulation Tasks

Curated research repository for the BSc (Hons) Digital Games Development dissertation:

**Real-To-Sim Learning for Dexterous Arm and Hand-Manipulation Tasks**

The research evaluates a real-to-simulation pipeline for training a physics-based virtual seven-degree-of-freedom arm and five-finger hand from captured human motion.

## Study design

Two learning conditions were compared:

- **Condition A: BC-only**  
  Behavioural Cloning from recorded demonstrations.

- **Condition B: BC-assisted PPO refinement**  
  Early Behavioural Cloning assistance followed by Proximal Policy Optimisation refinement.

Human hand intent was captured through vision-based hand tracking and converted into robot-compatible control values during demonstration recording.

## Final reported results

The submitted dissertation reports:

- BC-only: **0%** target-region success at the final reported Condition A checkpoint, approximately 1.206M logged steps.
- BC-assisted PPO: approximately **95%** target-region success under the fixed training layout.
- 6 cm cube-spawn offset: success fell to **19%**.
- 6 cm offset plus observation noise: **23%** success.
- Observation-noise-only conditions: **100%** success in both tested cases.

The success metric measures whether the cube enters the target placement radius. It does **not** require a stable autonomous release, so the demonstrated behaviour is better described as grasp-and-transport-to-target rather than full pick-place-release.

The dissertation completed one training run per condition, so the findings are descriptive rather than statistically generalisable.

## Technical environment

- Unity 2022.3.62f3
- Unity ML-Agents package 2.3.0-exp.3
- Unity Robotics URDF Importer 0.5.2
- Physics-based articulated robot environment
- KUKA iiwa-class seven-DOF arm with a five-finger hand model
- Custom ML-Agent, metrics, stress-test and episode-management tooling

## Public-repository scope

The original private research archive is roughly 770 MB and contains raw demonstrations, training outputs, ROS/URDF source material, thesis build files, debug logs and intermediate experiments.

This repository is intentionally a curated publication artifact. It will contain:

- original custom source code required to reproduce the experiment;
- exact training/evaluation configuration used for reported runs;
- compact result datasets and plots;
- reproduction instructions;
- the submitted dissertation PDF;
- attribution and licensing information for robot-description dependencies;
- selected demo media.

Large transient logs, IDE state, generated Unity data and unrelated thesis-build intermediates will remain outside the public repository.

## Reproducibility

The migration is still being completed from the private research archive. Before public release, the repository will document exact dependency versions, robot-model provenance, demonstration-data handling and the commands required to reproduce training and evaluation.

## License

No repository-wide license has been applied yet. Custom code, the dissertation text/data and third-party robot assets will be licensed or attributed independently before public release.
