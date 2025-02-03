---
uid: bind-c
---

# Interact with behavior graphs via C# scripts

Communication between behavior graphs and C# scripts is key to creating complex and responsive games. While behavior graphs provide a visual method to design complex behaviors, C# scripts provide control and detailed logic implementation.

You can use two techniques to integrate these two systems:

* [Direct binding to a `BehaviorGraphAgent` for instance-specific interactions](#use-c-binding-to-a-particular-instance-of-behaviorgraphagent)
* [Event Channel assets for global event propagation](#c-binding-using-event-channel-asset)

These methods enable GameObjects to share information and react to changes in real time.

## Use C# binding to a particular instance of `BehaviorGraphAgent`

You can bind directly to a `BehaviorGraphAgent` to subscribe to `BlackboardVariable` using the [`BehaviorGraphReference API`](https://docs.unity3d.com/Packages/com.unity.behavior@1.0/api/Unity.Behavior.BlackboardReference.html#methods).

You can then subscribe to the `BlackboardVariable.OnValueChanged` event or in case of an Event Channel, subscribe to `BlackboardVariable.Value.Event` event.

### Example code

```
[SerializeField] private BehaviorGraphAgent m_Agent;

// Agent state has changed to [value] (where value is of type StateExample)
private BlackboardVariable<StateEventChannel> m_stateEventChannelBBV;
private BlackboardVariable<StateExample> m_stateBBV;

private void OnEnable()
{
    if (m_Agent.BlackboardReference.GetVariable("StateEventChannel", out m_stateEventChannelBBV))
        m_stateEventChannelBBV.Value.Event += OnStateEvent;

    if (m_Agent.BlackboardReference.GetVariable("StateToReact", out m_stateBBV))
        m_stateBBV.OnValueChanged += OnStateValueChanged;
}

private void OnDisable()
{
    if (m_stateEventChannelBBV != null)
        m_stateEventChannelBBV.OnValueChanged -= OnStateChanged;
    if (m_stateBBV != null)
        m_stateBBV.OnValueChanged -= OnStateValueChanged;
}

private void Update()
{
    // your custom logic

    // Send event to the event channel of the referenced agent.
    // Only this instance of agent will receive it (except if the BlackboardVariable is 'Shared').
    m_stateEventChannelBBV.Value.SendEventMessage(StateExample.Alert);
}

private void OnStateEvent(StateExample value)
{
    // React to event
}

private void OnStateValueChanged()
{
    // React to state change
}
```

## C# binding using Event Channel asset

Event Channel assets allow for global communication across multiple behavior graphs or between graphs and C# components. You can generate an instance of an Event Channel to send and receive messages. This method is ideal when you need consistent event handling across systems.

Perform the following steps:

1. Create the Event Channel.
   1. Open the **Project** window.
   1. Right-click and select **Create** > **Behavior** > **Event Channel** > **[Channel Name]**.

   This creates an instance of that Event Channel that you can assign to the behavior graph and C# components.

1. To assign the Event Channel, in the C# component, use the Event Channel API to listen or send a new message.

### Example code

```
[SerializeField] private StateEventChannel m_EventChannel;

private void OnEnable()
{
    m_EventChannel.Event += OnStateEvent;
}
private void OnDisable()
{
    m_EventChannel.Event -= OnStateEvent;
}

private void Update()
{
    // your custom logic

    // Send event to the event channel instance.
    // All graphs and C# systems on that same event channel instance will receive it.
    m_EventChannel.SendEventMessage(StateExample.Alert);
}

private void OnStateEvent(StateExample value)
{
    // React to event
}
```
## Additional resources

* [Create a behavior graph](create-behavior-graph.md)
* [Create and manage variables and Blackboards](blackboard-variables.md)