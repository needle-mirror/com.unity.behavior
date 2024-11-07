---
uid: troubleshoot
---

# Troubleshooting issues with Unity Behavior

Solve common problems that might arise while working with Unity Behavior.

## Default values in RunSubgraph node not updating

When you change a subgraph's Blackboard Variable (BBV) default value, the RunSubgraph field retains its current value as an override instead of updating to the new default.

### Resolution: 

Revert the RunSubgraph field to its default value.

## Stack-overflow issue with `SharedBlackboardVariable`

A stack overflow occurs at runtime (or playmode) when the graph of a `BehaviorGraphAgent` attempts to access or set `SharedBlackboardVariable`. 

### Causes 

A Behavior editor crash might cause incorrect serialization of the `SharedBlackboardVariable` in `RuntimeBlackboardAsset`. 

### Resolution: 

Upgrade to Unity Behavior version 1.0.3 or later.

Additionally, check your original **Blackboards** to ensure no `SharedBlackboardVariable` is serialized in the `RuntimeBlackboardAsset`, as this can lead to issues later.

## Default value for `SharedBlackboardVariable` variable is null

A `SharedBlackboardVariable` on `Behavior(Graph)Agent` can sometimes have an invalid default value, resulting from the previous [stack overflow](#stack-overflow-issue-with-sharedblackboardvariable) issue.

### Causes

This problem is most likely caused by a `SharedBlackboardVariable` that's serialized on the `RuntimeBlackboardAsset` with an invalid value.

### Resolution

Perform the following checks to resolve this issue:

* Use Unity Behavior version 1.0.3 or later, because the `SharedBlackboardVariable` generation at authoring time is fixed in this version.

* Check the existing `RuntimeBlackboardAsset` to ensure it doesn't have any `SharedBlackboardVariable` serialized in their **Blackboard** > **Variables** field. If any is present, follow these steps to regenerate the **Variables**:

    1. Open the faulty behavior graph.

    2. Add a new **Blackboard** variable.

    3. Save the graph to refresh the asset.

    4. Delete the new variable.

    5. Save the graph again.

## Additional resources

* [Behavior graph node types](node-types.md)
* [Unity Behavior user interface](user-interface.md)