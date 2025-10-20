---
uid: serialize-ref-troubleshoot
---

# Troubleshooting invalid Behavior assets

Resolve invalid Behavior assets caused by missing `SerializeReference` types.

Behavior assets can become invalid when Unity loses references to serialized types. This usually happens when you refactor scripts, rename or move classes, or update packages that define node or variable types.

Invalid assets can’t open in the Behavior editor and might show placeholder nodes or missing Blackboard variable types.

## Symptoms

When Unity Behavior detects missing or invalid types, the Editor logs messages, such as:

* For missing node type:

    * The Authoring graph (`BehaviorAuthoringGraph`) shows the following message: `My Behavior Graph automatically updated nodes with missing type references: 1 node affected [...] converted to placeholder [...]`.

    * The Runtime graph (`BehaviorGraph`) shows the following warning: `My Behavior Graph stripped out placeholder nodes: 1 node affected [...]`.

* For resolved node type:

    * The Authoring graph shows the following message: `My Behavior Graph automatically updated nodes with missing type references: 1 node affected [...] resolved placeholder [...]`.

* For missing managed reference:

    * The Authoring asset (Graph or Blackboard) logs the following error: `The Behavior asset My Behavior Graph has missing types in its managed references. Inspect the asset for more information.`.

## Identify invalid assets

To identify assets with missing types in their serialization, use **Tools** > **Behavior** > **Check Assets Integrity** .

## Cause

This issue occurs when Behavior assets reference a type that no longer exists or has changed in the codebase.

Some common causes include:

* Class rename or move to a different namespace
* Package update or removal affecting node or Blackboard types
* Custom node class refactor or deletion
* Code pull from another branch or contributor that changed type definitions
* Compilation errors
* Missing or uninstalled required packages

When this happens, nodes and variables lose their type information and Unity replaces them with placeholders in authoring graphs.

## Resolution

Use one of the following methods to restore or repair missing types in your Behavior assets.

### Option 1: Restore missing types

If the issue is caused by refactoring or missing scripts, restore or recreate the types.

#### For custom nodes

1. Identify placeholder nodes in your Behavior graph.
2. Choose one of the following:

    * Delete placeholder nodes manually.
    * Select **Create** on a placeholder node to regenerate the missing node class.

#### For Blackboard variables

If possible, revert any class or namespace changes.

If you need to migrate or rename types, use one of these methods:

* If the issue affects a few classes, follow these steps:

    1. Create temporary stub classes with the original class name, namespace, and assembly. This unlocks the affected assets.
    2. Open affected assets.
    3. Recreate Blackboard variables and replace old references.
    4. Remove stub classes once all assets no longer contain Blackboard variables of the old type.

* If the issue affects many classes, edit the [YAML manually](#option-2-edit-yaml-manually) to repair the references.

### Option 2: Edit the YAML file manually

Use this method only if it's not practical to restore types or create stubs.

To edit the YAML file manually, follow these steps:

1. Back up your project or commit your work to version control.
2. Locate the affected `.asset` file in your **Project** window.
3. Right-click the asset and select **Show in Explorer** (Windows) or **Reveal in Finder** (macOS).
4. Open the file in a text editor.
5. Search for `type:` entries that reference missing or invalid types.
6. Replace the invalid `type:` entry with the correct class name and namespace.
7. Save the file and reopen Unity to reimport the asset.

    For example:

    ```
    # Before (invalid reference)
    type: {class: 'TypedVariableModel`1[[MyEventChannel, Assembly-CSharp]]', ns: Unity.Behavior, asm: Unity.Behavior}

    # After (corrected reference)
    type: {class: 'TypedVariableModel`1[[MigratedEventChannel, Assembly-CSharp]]', ns: Unity.Behavior, asm: Unity.Behavior}

    ```

## Prevention

To prevent invalid Behavior assets and safely recover from them, follow these practices.

* Prevention strategies:

    * Don't refactor classes used in Blackboard variables or serialized Behavior data.
    * Use version control and commit all working assets before making code changes.
    * Coordinate with your team when modifying scripts that affect serialized types.

* Recovery strategies:

    * Unity Behavior preserves data in an invalid state and it isn’t lost.
    * Follow recovery steps to avoid additional errors.
    * Consult your team or contact Unity community if the issues persist.

## Additional resources

* [Editor serialization in Unity Behavior](serialize-behavior.md)
* [Understand `SerializeReference` limitations](understand-limitations.md)
* [How `SerializeReference` limitations affect Behavior assets](behavior-assets-editor-serialization.md)
* [Mitigation systems in Behavior](mitigation.md)
