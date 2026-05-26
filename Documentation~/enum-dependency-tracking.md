---
uid: enum-dependency-tracking
---

# Enumeration dependencies tracking in authoring graphs

Enum dependencies in each authoring graph are tracked to detect enum changes even when graphs are closed.

## How it works
During graph validation, nodes that reference enums register those enum types with the authoring graph. Unity Behavior then computes and stores a signature for each enum dependency.

An enum signature includes:

* A stable enum type ID.
* The enum underlying numeric type.
* The enum member names and numeric values.
## What happens when an enum changes

On script reload, Unity Behavior compares current enum signatures against the signatures stored in graph assets.

If a mismatch is found, Unity Behavior:

1. Identifies all graphs that depend on that enum.
2. Reimports the affected authoring graph assets.
3. Triggers graph validation and refreshes serialized graph data.

This keeps authoring and runtime graph data synchronized with enum definitions after enum edits, renames, reordering, or numeric value updates.

## Warning message and recommended follow-up

When Unity Behavior detects enum signature changes, it logs a warning that lists the changed enum types and the number of graphs that will be reimported.

Example warning:

`Detected enum signature changes for 1 enum type(s). Changed enums: MyNamespace.MyEnum. Reimporting 3 behavior graph(s) to refresh serialized graph data. Use 'Tools/Behavior/Select Graphs Affected by Last Enum Change' to review impacted assets.`

Use the following menu actions to review impacted assets:

* `Tools/Behavior/Select Graphs Affected by Last Enum Change`
* `Tools/Behavior/Select Next Graph Affected by Enum Change`

After reimport, open the affected graph and verify the expected branch wiring and runtime behavior, especially for the nodes that expose enum members as named outputs.

## Switch port reconciliation behavior

When `Switch` (`SwitchComposite`) ports are refreshed, Unity Behavior reconciles existing connections in this order:

1. Match by enum member name.
2. If name no longer exists, attempt numeric-value fallback.
3. If neither name nor numeric value matches, treat as removed semantic and recreate ports as needed.

### Example 1: Name semantic takes priority

Patrol-state enum before:

`{ Idle = 0, Patrol = 1, Chase = 2 }`

Designer has logic connected to `Patrol`.
Then enum changes to:

`{ Idle = 0, Chase = 2, Patrol = 5 }`

Because the member name `Patrol` still exists, any logic attached to `Patrol` branch remains attached to it, even though its numeric value changed.

### Example 2: Numeric fallback when name is gone

Patrol-state enum before:

`{ Idle = 0, Patrol = 1, Chase = 2 }`

Designer has logic connected to `Patrol`.
Then enum changes to:

`{ Idle = 0, Investigate = 1, Chase = 2 }`

Because `Patrol` no longer exists, Unity Behavior tries numeric fallback and maps the existing branch to `Investigate = 1` (if unique). This usually matches an intended rename.

If this mapping is not desired, use the enum-change menu actions tools to locate affected graphs and update branches manually.

#### Note about using flag enumeration

Numeric fallback is applied only when the target numeric value maps to exactly one enum member.

If multiple members share the same numeric value (for example aliases such as `None = 0` and `Default = 0`, or flag aliases), fallback is ambiguous and Unity Behavior won't auto-remap that stale port by numeric value. This will result in any logic previously connected to not be preserved.

### Example 3: Name and numeric semantic both removed

Patrol-state enum before:

`{ Idle = 0, Patrol = 1, Chase = 2 }`

Designer has logic connected to `Patrol`.
Then enum changes to:

`{ Idle = 0, Chase = 2, Flee = 3 }`

No `Patrol` name exists and no value `1` exists. Unity Behavior treats that semantic as removed, so the logic previously to `Patrol` is not preserved.

## Additional resources

* [Editor serialization in Unity Behavior](serialize-behavior.md)
* [How `SerializeReference` limitations affect Behavior assets](behavior-assets-editor-serialization.md)
