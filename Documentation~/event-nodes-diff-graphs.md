---
uid: event-nodes-diff-graphs
---

# Integrate event nodes within and across behavior graphs

Event nodes enhance interaction between different game elements. Use this information to set up event nodes for communication within the same behavior graph or between multiple graphs. Event nodes rely on event channels to send and receive messages.

## Use an event channel within the same graph

To send and receive events within the same behavior graph, follow these steps:

1. Add the **Send Event Message** and **Wait for Event Message** (or **Start On Event Message**) nodes in your graph at the relevant locations. 

2. To create a new event channel, select **Blackboard** > **+** > **Events** > **Create new event channel type**.

   For more information on how to create an event channel, refer to [Create an event channel](event-nodes.md#create-an-event-channel).

   An event channel appears as a variable on the **Blackboard**. 

3. Manually link the variable to the event nodes.

Behavior automatically generates the necessary event channel `ScriptableObject`.

## Use an event channel between multiple graphs

For games with agents that require communication between graphs, you can share event channels across different behavior graphs.

To set up communication between agents, follow these steps:

1. On the graph that sends the event message, add a **Send Event Message** node.  

2. On each behavior graph that intends to listen for this event channel, add a **Wait for Event Message** (or **Start On Event Message**) node.

3. Create the event channel using step 2 of the [Use an event channel within the same graph](#use-an-event-channel-within-the-same-graph) section.

4. To create a shared event channel `ScriptableObject`, follow these steps:

   1. From the **Project** view, select **Create** > **Behavior** > **Event Channels**.
   2. Select the event channel you created in step 3. 
   
5. Assign the previously created event channel `ScriptableObject` as the value of the event channel variable you created earlier. You can do this either from the graph's **Blackboard** (so all instances of the graph use the same event channel by default) or individually for each relevant Behavior agent component. 

This setup allows one agent or behavior graph to send messages that the other agent or behavior graph receives through the shared event channel.

## Additional resources

* [Use event nodes](event-nodes.md) 
* [Event nodes example](event-nodes-example.md)