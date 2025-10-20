---
uid: serialize-behavior
---

## Editor serialization in Unity Behavior

Unity Behavior stores nodes and Blackboard variables in assets using `SerializeReference`, which are then saved as `ScriptableObjects`. This section explains how serialization works, its limitations, and how to identify or prevent issues that missing type references cause.


| Topic | Description |
| ----- | ----------- |
| [Understanding `SerializeReference` limitations](understand-limitations.md) | Learn when Unity’s `SerializeReference` can lose a referenced type and why that matters for assets. |
| [How `SerializeReference` limitations affect Behavior assets](behavior-assets-editor-serialization.md) | Understand how lost type references affect nodes and Blackboard variables. |
| [Mitigation systems in Behavior](mitigation.md) | Review Editor, Play mode, and build-time safeguards that reduce the risk and impact of missing types.|

## Additional resources

* [Serialization in Unity Behavior](manage.md)
* [Create with Unity Behavior](get-started.md)
