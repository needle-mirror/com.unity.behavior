---
uid: troubleshoot-default-value
---

# Troubleshooting Blackboard and RunSubgraph issues

Resolve common issues related to Blackboard variables and RunSubgraph nodes in Unity Behavior.

These issues include incorrect default values, stack overflow errors, and null `SharedBlackboardVariable` values in Behavior agents.

## Issue 1: RunSubgraph node default value doesn't update

### Symptoms

When you change a subgraph’s Blackboard Variable (BBV) default value, the **RunSubgraph** field keeps the previous value instead of updating.
The **Inspector** shows the outdated value as an override.

### Cause

The field stores its current value as an override rather than applying the new Blackboard default.

### Resolution

Revert the RunSubgraph field to its default value.

## Issue 2: Stack-overflow error with `SharedBlackboardVariable`

### Symptoms

A stack overflow error occurs in Play mode or at runtime.
Unity logs reference `SharedBlackboardVariable` or `RuntimeBlackboardAsset`.

### Cause

A crash in the Behavior Editor can serialize invalid `SharedBlackboardVariable` data in the `RuntimeBlackboardAsset`.

### Resolution

Upgrade to Unity Behavior version 1.0.3 or later.

Check your original **Blackboards** to ensure no `SharedBlackboardVariable` is serialized in the `RuntimeBlackboardAsset`.

## Issue 3: `SharedBlackboardVariable` default value is null

### Symptom

A `SharedBlackboardVariable` on `Behavior(Graph)Agent` shows a null default value. This issue might appear after you fix a previous [stack overflow](#issue-2-stack-overflow-error-with-sharedblackboardvariable) problem.

### Causes

The `SharedBlackboardVariable` was serialized in `RuntimeBlackboardAsset` with an invalid default value.

### Resolution

Perform the following checks to resolve this issue:

* Update to Unity Behavior version 1.0.3 or later.
* Check that no `SharedBlackboardVariable` appear under **Blackboard** > **Variables** in `RuntimeBlackboardAsset`.
* To refresh serialization, do the following:

    * Open the faulty behavior graph.
    * Add a new **Blackboard** variable.
    * Save the graph to refresh the asset.
    * Delete the new variable.
    * Save the graph again.

## Additional resources

* [Editor serialization in Unity Behavior](serialize-behavior.md)
* [Understand `SerializeReference` limitations](understand-limitations.md)
* [How `SerializeReference` limitations affect Behavior assets](behavior-assets-editor-serialization.md)
* [Mitigation systems in Behavior](mitigation.md)
