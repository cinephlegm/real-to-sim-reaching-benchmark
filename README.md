# Real-to-Sim Learning for Dexterous Arm and Hand-Manipulation Tasks

BSc (Hons) Digital Games Development dissertation project by **Josef Carl Muscat**.

A Unity ML-Agents experiment using captured human hand motion to train a simulated **KUKA iiwa7 + SCHUNK SVH** manipulation system.

## Experiment

Human hand motion is captured from a Kinect v2 colour stream with MediaPipe, converted into robot hand controls, and sent to Unity over UDP. The resulting demonstrations are used to compare two training conditions:

| Condition | Training |
| --- | --- |
| A | Behavioural Cloning only |
| B | Behavioural Cloning followed by PPO refinement |

The final agent uses **27 observations**, **13 continuous actions**, and a dataset of **60 recorded demonstrations**.

Exact trainer settings are kept in [`training/`](training/).

## Known implementation limitation

The MediaPipe capture script computes finger-joint angles from normalized image landmarks. Because MediaPipe normalizes image `x` and `y` against different frame dimensions, those coordinates are not isotropic. This can introduce orientation-dependent error into the derived finger-curl values.

This behaviour is preserved here because it was part of the implementation used to record the submitted demonstration dataset. Changing it now would make the published capture code differ from the experiment that produced the dissertation results.

## Findings

Behavioural Cloning learned parts of the demonstrated behaviour but did not complete the target-region task reliably. PPO refinement produced reliable grasp-and-transport behaviour in the fixed training layout.

The robustness test also exposed the main limitation: changing the cube's starting position caused a large performance drop, while the tested observation noise alone had little effect.

The evaluation measures **target-region entry**, not a controlled release, so this should not be described as full autonomous pick-and-place.

## Repository

```text
capture/    Kinect v2 / MediaPipe hand capture and UDP sender
data/       demonstrations, training summary and robustness dataset
paper/      submitted dissertation PDF
training/   ML-Agents configurations for both conditions
unity/      custom Unity scripts and exact package metadata
```

The original Unity archive also contained generated files, debug tooling, raw training output and third-party robot assets. Those are intentionally not carried into this repository.

## Environment

- Unity **2022.3.62f3**
- ML-Agents **Release 20**
  - Unity package: `com.unity.ml-agents 2.3.0-exp.3`
  - Python trainer: `ml-agents 0.30.0`
- Unity Robotics URDF Importer **v0.5.2**
- MediaPipe Hand Landmarker
- Kinect v2 via PyKinect2, with webcam fallback

The MediaPipe model is not stored in the repository. [`capture/mediapipe_sender.py`](capture/mediapipe_sender.py) downloads the model from the official MediaPipe model host when required.

## Robot and software sources

The robot descriptions are **not redistributed here**. Obtain them from their original sources.

- **KUKA iiwa7 description**  
  [IFL-CAMP/iiwa_stack](https://github.com/IFL-CAMP/iiwa_stack)  
  The `iiwa_description` package was used as the KUKA model source. The upstream repository is BSD-2-Clause licensed.

- **SCHUNK SVH hand description**  
  [touchlab-avatarx/schunk_svh_description](https://github.com/touchlab-avatarx/schunk_svh_description)  
  Used as the SVH hand model source. Its upstream license is proprietary, so the model, meshes and URDF files are not included in this repository.

- **Unity URDF Importer**  
  [Unity-Technologies/URDF-Importer](https://github.com/Unity-Technologies/URDF-Importer)  
  Version used: [v0.5.2](https://github.com/Unity-Technologies/URDF-Importer/releases/tag/v0.5.2).

- **Unity ML-Agents**  
  [Unity-Technologies/ml-agents](https://github.com/Unity-Technologies/ml-agents)  
  The project uses the Release 20 package pairing: Unity package `2.3.0-exp.3` and Python package `0.30.0`.

- **MediaPipe hand tracking**  
  [google-ai-edge/mediapipe](https://github.com/google-ai-edge/mediapipe)  
  Hand tracking follows the MediaPipe Hands / Hand Landmarker pipeline described by Zhang et al., *MediaPipe Hands: On-device Real-time Hand Tracking* ([arXiv:2006.10214](https://arxiv.org/abs/2006.10214)).

- **Kinect v2 Python interface**  
  [Kinect/PyKinect2](https://github.com/Kinect/PyKinect2)

## Data

[`data/results_summary.csv`](data/results_summary.csv) contains the compact training summary used for the A/B comparison.

[`data/stress_test_results.csv`](data/stress_test_results.csv) contains the **800 inference episodes** from the eight-condition robustness evaluation.

[`data/demos/`](data/demos/) contains the **60 ML-Agents demonstration files** used by both training conditions.

## Dissertation

[Read the submitted dissertation (PDF)](paper/dissertation.pdf).

## Citation

If you use this repository or its data, cite the dissertation.

### IEEE

```text
J. C. Muscat, "Real-To-Sim Learning for Dexterous Arm and Hand-Manipulation Tasks," B.Sc. (Hons) dissertation, Institute of Information and Communication Technology, MCAST, Malta, 2026.
```

### Harvard

```text
Muscat, J.C. (2026) Real-To-Sim Learning for Dexterous Arm and Hand-Manipulation Tasks. BSc (Hons) dissertation. MCAST Institute of Information and Communication Technology.
```

### APA

```text
Muscat, J. C. (2026). Real-To-Sim Learning for Dexterous Arm and Hand-Manipulation Tasks [BSc (Hons) dissertation, MCAST Institute of Information and Communication Technology].
```

### BibTeX

```bibtex
@thesis{muscat2026realtosim,
  author      = {Josef Carl Muscat},
  title       = {Real-To-Sim Learning for Dexterous Arm and Hand-Manipulation Tasks},
  type        = {BSc (Hons) dissertation},
  institution = {MCAST Institute of Information and Communication Technology},
  year        = {2026}
}
```

GitHub can also read the repository citation metadata from `CITATION.cff`.
