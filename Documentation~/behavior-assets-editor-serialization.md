---
uid: behavior-assets-editor-serialization
---

# How `SerializeReference` limitations affect Behavior assets

In Unity Behavior, both nodes and Blackboard variables use managed references for serialization. When a referenced type becomes unavailable, such as when you rename, move, or delete a class, Unity marks those references as missing. This can cause issues in Behavior assets that depend on those references.

The most common cause of type loss is code refactoring, such as when you rename or move a class to a new namespace. It can also occur when you import or update third-party packages or assets that change type definitions.

These issues can affect both nodes and Blackboard variables in Behavior assets.

## Nodes

Nodes in Behavior graphs rely on serialized references to maintain type information and behavior definitions. To prevent data loss when references break, Behavior uses two validation mechanisms:

* Type and ID tracking: Authoring graphs store a list of `NodeModelInfo` objects that include both the node type (from the last serialization) and the `NodeDescription` ID. You can rename or move a node type to another namespace as long as its ID remains unchanged.

* Placeholder nodes: If you remove a node script or change a `NodeDescription`, the affected `NodeModelInfo` becomes a placeholder. The Authoring graph (part of the Behavior graph) displays this placeholder visually, while the Runtime graph rebuilds itself to exclude the missing node types.

These mechanisms ensure that graphs remain usable even when individual node types are missing.

## Blackboard variables

Blackboard variables don't provide a placeholder mechanism. When the type wrapped in a Blackboard variable is lost, the asset must resolve that reference before it can function again.

A lost Blackboard variable type can affect the following areas:

* Blackboard asset that stores variables.
* Nodes such as Action, Modifier, or Composite nodes that reference those variables.
* Condition nodes that use Blackboard variables as input or output fields.

If you use a graph with a missing type as a static subgraph, its parent graph must also resolve the missing reference.

## Additional resources

* [Understand `SerializeReference` limitations](understand-limitations.md)
* [Mitigation systems in Behavior](mitigation.md)
