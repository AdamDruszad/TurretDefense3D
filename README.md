# Turret Defense 3D

Reinforcement learning project for the *Introduction to Reinforcement Learning* course (University of Debrecen).

A turret agent learns to aim (yaw + pitch) and shoot flying drones that move randomly in a 3D arena, trained with Unity ML-Agents (PPO).

## Highlights
- 3D environment with moving, randomized targets
- Continuous actions (yaw, pitch) + discrete shoot action
- RayPerceptionSensor3D combined with vector observations
- Parallel training arenas
- Custom YAML config with curriculum learning
- Hyperparameter comparisons in TensorBoard

## Setup
- Unity 6 (6000.6) + ML-Agents package
- Python 3.10.12, `mlagents==1.1.0`

## Training
```
conda activate unity
cd Training
mlagents-learn turret_config.yaml --run-id=RUN_NAME
tensorboard --logdir results
```
