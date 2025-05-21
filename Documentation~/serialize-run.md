---
uid: serialize-run
---

# Set up and run the sample

The **Runtime Serialization** sample shows how to save and load the full state of behavior agents at runtime. 

Use this section to do the following:

* [Import the sample](#import-and-open-the-sample-scene)
* [Explore its contents](#import-and-open-the-sample-scene)
* [Run the scene](#run-the-sample)
* [Test serialization by demonstrating how agent positions and their behavior graph states are preserved across play sessions](#run-the-sample)

## Import and open the sample scene

To use the **Runtime Serialization** sample, follow these steps:

1. Open the **Package Manager** window.
1. Select **In Project** > **Behavior** > **Samples**.
1. Select **Import** next to the **Runtime Serialization** sample.
1. To locate the sample components, go to **Project** > **Assets**> **Sample** > **Behavior** > **[version-number]** > **Runtime Serialization**. 

The **Runtime Serialization** folder contains the following components:

| Component | Name |
| --------- | ---- |
| Scene | `Serialization.unity` |
| Behavior graph | `Agent.graph` |
| Prefabs | `Agent` and `Weapon` |
| Materials | Two materials for visuals |
| Scripts | * `SerializationExampleSceneController.cs`<br> * `ChooseTargetPosition.cs` <br> * `GameObjectResolver.cs` (embedded example) |

## Run the sample

The **Runtime Serialization** sample shows how to save and restore agent state at runtime.

To run the `Serialization.unity` scene, follow these steps:

1. Double-click the `Serialization.unity` scene to open it. 
1. Enter Play mode.

   The scene shows 15 capsule agents that move around wielding swords. 

1. Select **Save** to serialize the current state of all the agents.
1. Select **Load** to restore that state at any time, even after you exit and re-enter Play mode.

Runtime serialization restores the agent positions and saves the full state of each agent's behavior graph. When you reload the graph, the agents resume their behavior from the exact point they left off.

## Additional resources

* [How behavior graph works](xref:serialize-work)
* [Scripts and serialization architecture](xref:serialize-scripts)