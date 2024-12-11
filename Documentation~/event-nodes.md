---
uid: event-nodes
---

# Use event nodes

Use event nodes to send and receive messages within behavior graphs. Event nodes perform the following functions:

* Coordinate different parts of a behavior graph. For example, you can use an event node to raise an event when a specific trigger occurs, such as the agent detects an enemy or completes a task. This event notifies other parts of the graph to update the state or perform a specific action.
* Connect behavior graphs with C# components. Similar to behavior graphs, C# scripts can both broadcast and listen to event channels.
* Decouple logic from root graph evaluation. For example, if something happens, continue to perform your current action but start to prepare the next action in the background.

To access the event nodes, select **Add** > **Events**. Unity Behavior provides the following event nodes by default:

* [On Start](#on-start)
* [Send Event Message](#send-event-message)
* [Start On Event Message](#start-on-event-message)
* [Wait for Event Message](#wait-for-event-message)

## On Start

The **On Start** node is the entry point of a behavior graph. It activates immediately when the graph begins to run, specifically during the first update frame of BehaviorGraphAgent.

You can have multiple **On Start** nodes in a single behavior graph to allow multiple branches to run in parallel. Each **On Start** node has a **Repeat** option to re-trigger the actions connected to the behavior graph multiple times. 

> [!NOTE]  
> In Unity Behavior, running in parallel refers to running sequentially from left to right, rather than concurrently in a multithreaded manner.

## Send Event Message

The **Send Event Message** node broadcasts an event message over an event channel.

To use the **Send Event Message** node, you must first [create and configure the event channel](#create-an-event-channel) to be a pathway for event messages.

### Create an event channel

An event channel is a [Scriptable Object](https://docs.unity3d.com/Manual/class-ScriptableObject.html) that receives and sends parametric event messages. It uses events to facilitate communication between different parts of the behavior graph.

To use event nodes that send or receive event messages, such as [**Send Event Message**](#send-event-message), [**Start On Event Message**](#start-on-event-message), and [**Wait for Event Message**](#wait-for-event-message), you must create or select an event channel. This channel ensures these nodes can coordinate actions based on the event messages.

For example, an agent spots an enemy and sends an `EnemySpotted` event using an event channel. Other parts of the graph that listen to the same channel can respond appropriately, such as start combat actions or alert allies. 

To create an event channel, follow these steps:

1. Right-click any empty area of the Unity Behavior graph editor and select **Add** > **Event** > **Send Event Message**.

1. Do one of the following:

   * Select the link icon in the **Event Channel** field of the **Send Event Message** node and then select **Create Event Channel**.

   * Select the **+** icon on the **Blackboard**, select **Events**, and then select **Create new event channel type**. 

1. In the **New Event Channel** window, enter the **Name** of the event channel. For example, `Enemy Detected`.

1. Enter a description for the event channel and select the appropriate variable from the list for each element.

1. Select **Create**.

   The new event channel variable displays on the **Blackboard**.

1. Select the link icon in the **Event Channel** field of the **Send Event Message** node and then select the new event channel variable from the **Link Variable** window.

   The newly created event channel is now assigned to the **Send Event Message** node.

## Start On Event Message

The **Start On Event Message** node listens for a specific event message and triggers a sequence of actions when it receives that event message.

## Wait for Event Message

The **Wait for Event Message** node waits until it receives a specific event message. It then triggers the next sequence of actions in the behavior graph.

> [NOTE]
> The **Event Channel** field in this node retrieves the value from the incoming message. However, it doesn't enforce conditions or ensure that the graph only resumes when the message meets specific criteria.

## Additional resources

* [Event nodes example](event-nodes-example.md)
* [Create a custom node](create-custom-node.md)