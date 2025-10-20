---
uid: understand-limitations
---

# Understand `SerializeReference` limitations

The `SerializeReference` attribute serializes polymorphic references, but it has limitations when the referenced type becomes unavailable. When this happens, Unity Behavior marks assets as having `Managed references with missing types`.

A type loss can occur in the following cases:

* When you rename, move, or delete classes to refactor a code.
* Assembly changes due to compilation issues or missing dependencies.
* Different team members use different package or code versions.

For more information about `SerializeReference`, refer to the [Unity’s `SerializeReference`](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/SerializeReference.html) documentation.

## Additional resources

* [How `SerializeReference` limitations affect Behavior assets](behavior-assets-editor-serializationavior-assets.md)
* [Mitigation systems in Behavior](mitigation.md)
