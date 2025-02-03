---
uid: behavior-agent
---

# Add and set up Behavior Agent component

The Behavior Agent component runs `BehaviorGraph` on a GameObject in the Unity scene. Use it to override the exposed Blackboard variables with values specific to that instance.

The Behavior Agent component provides APIs to manage its operation, including:

* **Pause or resume activity**: Enable or disable the Behavior Agent component to pause or resume its operations.
* **Restart the graph**: Reset and restart the behavior graph to its initial state.
* **Manage Blackboard variables**: Retrieve and set Blackboard variable values.

For more information, refer to the relevant [API documentation](https://docs.unity3d.com/Packages/com.unity.behavior@1.0/api/Unity.Behavior.BehaviorGraphAgent.html).

> [!NOTE]

> When using the Behavior agent component to manage `BehaviorGraph` on a GameObject, it's important to consider how you initialize agents that must start in a disabled state. If you want to create a Behavior agent that's initially disabled, you must explicitly disable the `BehaviorGraphAgent` at the time of creation. Later, when you want the agent to begin processing, you can enable it.

To add and set up the Behavior Agent component, follow these steps:

1. In the Unity Editor, select the GameObject to which you want to add the component.
2. In the **Inspector** window, select **Add Component**.
3. In the list, search for and select **Behavior Agent**. 
4. In the **Inspector** window for the Behavior Agent component, locate the **Behavior Graph** field.
5. Drag a pre-configured behavior graph into this field or select one using the object picker.

   Alternatively, you can drag a behavior graph asset from the **Project** window onto the GameObject in the **Hierarchy** window. Unity automatically adds the Behavior Agent component and links the graph.

6. To test or preview how the graph logic changes the way the GameObject behaves, enter Play mode.

## Additional resources

* [Create a behavior graph](create-behavior-graph.md)
* [Save and load running graph state](serialization.md)