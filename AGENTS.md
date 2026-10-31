# Repository instructions

This repository is the curated public artifact for the dissertation "Real-To-Sim Learning for Dexterous Arm and Hand-Manipulation Tasks."

## Research integrity

- The submitted dissertation is the authoritative source for reported methodology, metrics and conclusions.
- Do not replace final reported results with values from stale/intermediate training folders.
- Clearly distinguish original dissertation experiments from later reproductions or extensions.
- Preserve the distinction between target-region success and full stable pick-place-release.
- Preserve the limitation that one training run was completed per condition.

## Source migration

- Copy only custom source and genuinely required reproducibility material from the private archive.
- Do not copy debug logs, IDE state, thesis build intermediates or giant raw training folders without a documented reason.
- Third-party URDF, mesh and robot-description assets must retain their original licensing and attribution.
- Do not assume all files under the old RobotSources directory share the same license.

## Reproducibility

Any change to experimental behaviour must document:

1. Unity and ML-Agents versions;
2. observation and action spaces;
3. reward shaping;
4. demonstration dataset version;
5. training configuration;
6. evaluation layout;
7. perturbation/noise conditions;
8. whether the result is original or newly reproduced.

## Safety and provenance

- Never commit credentials, machine-specific paths or private participant/user data.
- Prefer scripts that regenerate derived plots/tables from compact source data.
- Keep raw experimental archives private unless there is a clear reproducibility benefit and redistribution is permitted.
