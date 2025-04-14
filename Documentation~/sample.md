---
uid: sample
---
# Import Behavior samples

Unity Behavior provides sample content to help you get started with its features. The samples include reference assets and examples to demonstrate key features, such as agent decision-making and runtime serialization. Use the samples to create and customize behavior graphs in Unity.

## Available samples

Unity Behavior includes the following samples:
   
* **Unity Behavior Example**: Provides a demo scene and predefined example actions.

* **Runtime Serialization**: Provides an example of how to save and restore behavior graph states during runtime.

## Import the samples

To use the samples provided with Unity Behavior, perform the following steps:

1. Open the **Package Manager** window.
2. Select the **In Project** tab. 
3. From the package list, select the **Behavior** package.
4. From the packages details panel, select **Samples**.

   The **Samples** tab lists the following samples:
   
   * **Unity Behavior Example**: Provides a demo scene and predefined example actions.

   * **Runtime Serialization**: Demonstrates how to save and restore behavior graph states during runtime.

5. Click **Import** next to the sample you want to import in your project. 

    * **Unity Behavior Example**: Unity creates a `Unity Behavior Example` folder in the **Project** window at `Assets` > `Samples` > `Behavior` > `[Version Number]`.

    * **Runtime Serialization**: Unity creates a `Runtime Serialization` folder in the **Project** window at `Assets` > `Samples` > `Behavior` > `[Version Number]`.

The following sections explain each sample and how to use them.

## Use Unity Behavior Example

The **Unity Behavior Example** sample demonstrates a simple decision-making flow for an agent. When the graph starts, the agent repeats the sequence. It begins by asking, `What should I do?` and then makes a random decision between two options. In the first option, the agent responds with `Let me think about it,` waits for one second, and then loops back to the start. In the second option, the agent announces, `Move to a target location!`, selects a random target from a tagged group of objects, receives a response from the chosen target, and navigates to it.

To explore the **Unity Behavior Example** sample, follow these steps:

1. Drag the sample scene to the **Hierarchy** window. The sample scene is in **Project** > `Assets` > `Samples` > `Behavior` > `[Version Number]` > `Unity Behavior Example` > `SampleScene.unity`.

2. Click the example behavior graph to open it. The graph is in **Project** > `Assets` > `Samples` > `Behavior` > `[Version Number]` > `Unity Behavior Example` > `Example Graph.asset`.

3. To view the logic and decision-making flow that the agent follows, open the example behavior graph by double-clicking `Example Graph.asset`

You can examine the node structure, understand how events and actions are connected, and modify or extend the graph to suit your project needs.

   ![Sample graph](Images/Sample-Scene-Graph.png)

## Use Runtime Serialization

The **Runtime Serialization** sample includes example scripts and a behavior graph. 

To use the **Runtime Serialization** sample, use the scripts and behavior graph in **Project** > `Assets` > `Samples` > `Behavior` > `[Version Number]`.

## Additional resources

* [Unity Behavior editor user interface](user-interface.md)
* [Create a behavior graph](create-behavior-graph.md)