---
uid: assets
---

# Work with behavior graph assets

In Unity, behavior graphs use two distinct but related asset types: `BehaviorAuthoringGraph` and `BehaviorGraph`. 

The separation of these assets is intentional to optimize performance and maintain the necessary editor functionality.

* `BehaviorAuthoringGraph` contains editor-specific model data that is used to visualize and edit behavior graphs.
* `BehaviorGraph` is a runtime representation of the authoring data.
* Each agent requires its own `BehaviorGraph` instance to track independent operation state.

## Asset types

Each asset type serves a unique role in the workflow.

### `BehaviorAuthoringGraph`

The key features of the `BehaviorAuthoringGraph` asset type are as follows:

* The editable, authoring/design-time representation of your behavior graph.
* Contains additional debug information and editor-specific data.
* Visible in the **Project** window as the primary graph asset you create and edit.
* An internal class that can't be directly referenced in gameplay code or components.

### `BehaviorGraph`

The key features of the `BehaviorGraph` asset type are as follow: 

* The runtime representation of your behavior graph.
* Optimized for performance by the removal of editor-specific data.
* Exists as a subasset of `BehaviorAuthoringGraph`.
* The only graph type that must be referenced in gameplay code and components.

## Common issues

When you work with behavior graph assets, you might encounter issues related to asset assignment and runtime operation.

### Assignment compatibility

If a field requires `BehaviorGraph`, you can't directly assign `BehaviorAuthoringGraph` to it because both these asset types perform different roles. The `BehaviorGraphAgent` component extracts the runtime graph from an authoring graph, but this capability isn't general purpose.

### Finding the runtime graph

To assign a `BehaviorGraph` to a field, follow these steps:

1. Locate the `BehaviorAuthoringGraph` in the **Project** window.
1. Select the arrow next to the asset to expand it. 
1. Find and assign the nested `BehaviorGraph` subasset.

## Best practices

Follow these best practices for authoring and runtime.

* **In the editor**: Use `BehaviorAuthoringGraph` within the behavior graph editor to author and visualize.
* **In your code**: Reference only `BehaviorGraph` assets for runtime operation. The asset should only act as an original reference, and you should create an instance of it (using `ScriptableObject.Instantiate`) before running it.
* **For components**: Use `BehaviorGraphAgent` when possible, as it handles the correct setup and instance management.

## Additional resources

* [Interact with behavior graphs via C# scripts](bind-c.md)
* [Debug the Agent in real time during Play mode](debug.md)