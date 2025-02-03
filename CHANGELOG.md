
All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [1.0.8] - 2025-02-03

### Changed
- Switch node will no longer log errors for unassigned children.
- Updated sample materials to URP (the default render pipeline for Unity 6.0).
- `Start()` will start the graph again if it's not currently running (i.e. it finished, or just been set). It'll defer it to `Update()` if the component is disabled.
- Components will now appear under the `Other/MonoBehaviours` search menu for variable types (adding new variables, or setting them for node variables).
- Updated AppUI to 2.0.0-pre.16 to benefit from fixes to AppUI.

### Fixed
- Fixed an issue where existing subgraph representations would not sometimes be removed when deleting the text input.
- Static subgraphs now have their parent rebuilt when saved.
- Fixed regression where renaming a blackboard variable would not be reflected in the graph.
- Calling `BehaviorGraphAgent.Restart()` before the graph was started resulted in the graph's `Start()` being called twice.
- Fixed search view string formatting leaking through to the callers such as type dropdowns.
- Fixed an issue where case differences in the case of variable names on the subgraph representation could cause duplicate fields added on a `SubgraphNodeModel`.
- Opening a behavior graph window wasn't focusing the window when docked, causing it to be behind other windows.
- Graph agent init and start states weren't set correctly after runtime deserialization.
- `Restart()` was running first iteration of the graph even when the agent was paused.
- Fixed behavior graph not always framing the graph content correctly when opening a Behavior Graph asset.
- Fixed attempting to assign a GameObject to a newly created GameObject Blackboard Variable in the Blackboard inspector not working.
- Fixed an issue where Run Subgraph node LinkFields would not keep their override values correctly when the Subgraph Representation mode was toggled on.
- Fixed an issue where duplicated Run Subgraph nodes did not keep their Subgraph Representation mode selected for the node UI.
- Right Click -> Edit/Inspect Script will now work correctly with VSCode.
- Fixed warning when changing a BlackboardVariable type to Vector2Int or Vector3Int.
- Fixed search filter formatting persisting when using the back button to return to the search root.
- Improved search query result accuracy.
- Overriding 'Self' variable works again, allowing you to use a different GameObject to the one running the graph.
- `NavigateToTargetAction` will correctly return success instead of failure if it's started within range of the target.
- Fixed an issue where fields on duplicated nodes did not keep the correct local values from the original.
- Sequencing/Enum Wizards will no longer propose the same enum/port name as one that already exists (example, adding 2 members with default name, deleting the 1st one and adding a new one used to call both Member 2).
- Fixed BlackboardAsset Shared Variable keeping playmode value.
- Run Subgraph (Dynamic) wasn't initializing a copy of the graph asset correctly, causing data to be shared amongst agents sharing the graph and corrupting the graph asset.
- Various `SetBlackboardVariable` APIs were writing to the asset incorrectly before it was initialized.
- Changing the value of a subgraph blackboard variable to a different subgraph will now correctly update the dynamic subgraph node running it.
- Subgraph variables will correctly sync with the parent graph and vice versa.

### Known Issues
- After much user feedback and investigation, we have determined that runtime serialization is not recommended for use at this time due to an existing limitation in its implementation. Our team is working diligently to address these concerns and bring the feature to the expected quality level in an upcoming release. In the meantime, we appreciate your patience and understanding as we strive to enhance this functionality.
- After using the "Delete Runtime Assets" button for the graph, prefabs/objects using the graph may lose their references to it (or contain an old stale one) and need reassignment.

## [1.0.7] - 2024-12-11

### Added
- Added new Interrupted status and ensure that all incomplete child nodes of Parallel composites are marked as interrupted.
- Added a checkbox to the behavior project settings to contnrol if nodes and condition scripts should automatically open after creation.

### Changed
- Selection of a node now brings it to the front.
- Selection of a node in a sequence brings the sequence to the front.
- Small tweak to node selected border radius.
- Nodes replaced with Placeholder Nodes will not be replaced in the asset, only in the UI, reducing risk in recovery.
- When saving a new Node/Condition from the wizard, a postfix of the node type (Action/Modifier/Flow) or Condition won't be added to the suggested filename if it already ends with it.
- Updated AppUI dependency to 2.0.0-pre14 and created a settings file to exclude AppUI Shaders and execution from builds. This can be overriden with a custom user App UI Settings file.
- Exposed `SaveFolderEventChannels` in the Project Settings -> Behavior menu.
- Improved documentation description for the `GameObject` API inside the `Node` class.
- Improved asset rebuilding and saving mechanism to avoid frequent regeneration.

### Fixed
- Editing Subgraph Representation will no longer add duplicate variables.
- Softened search fields filtering so that, for example, 'int list' and 'list int' will return 'Integer List'.
- Fixed WaitForAnyComposite not ending correctly yielding multiple runs of child nodes. 
- Editing a node with the node wizard (edit node definition) will no longer remove to Action/Modifer/Sequence postfix.
- Fixed an issue where automatic save path for enums and event channels saved the file name into the save path settings and caused an infinite loop when saving the next file.
- Remove erroneous references to Muse in the API docs.
- Improved Undo support for editing Behavior Agent on a GameObject.
- Adding a Run In Parallel node and its various types from the branch dialog will set the correct mode.
- Subassets of Graph and Blackboard will have their names track more correctly with the main asset.
- BehaviorWindow will now close during autosave if its asset is invalid.

### Known Issues
- Deleting a behavior graph asset while the window is open can cause the graph editor window to be stuck and the user will need to reset their layout to get rid of it.
- Runtime serialization of nodes in user assemblies don't serialize some properties correctly.
- Editing fields in Run Subgraph node with subgraph representation doesn’t update the inspector values.

## [1.0.6] - 2024-11-20

### Added
- Search popups will show paths for items when a search query was given. A tooltip will be shown when hovered on the path text in case it's truncated.
- Project settings option to set the save locations for nodes, conditions, enums and event channels.
- Fields with an asset assigned to them will ping the item on click and select the item on double click.
- Behavior window will no longer block Shift+Space shortcut to maximize the window when docked.

### Changed
- Reduce GC allocations related to condition nodes `CheckConditions`.
- Reduce GC allocations related to condition nodes `Evaluate` where we know the type of the object we're comparing.
- Changed single quotation marks in dialogues to double quotation marks.
- Changed single quotation marks in node tooltips to double quotation marks.
- Changed single quotation marks in logs to double quotation marks.
- Allow selecting a ScriptableObject from the Assets without having to make a blackboard variable.

### Fixed
- Fixed the graph asset not being reloaded on window focus if changed externally.
- Event channel generation will correctly add the relevant namespaces for variables added.
- Save location wasn't correctly remembering the last location saved.
- Behavior Graph Agent Inspector should no longer shows unexposed variables during play mode.
- RuntimeBlackboardAsset will correctly save its variables on entering play mode and reset them on exiting play mode.
- Fixed choosing an enum value from a blackboard variable dropdown not mapping correctly to assigned enum values.
- A small hang when debugging if a graph's view was enabled & disabled many times.
- When debugging the first click when selecting an agent failed.
- The Abort node now checks its OnStart condition to prevent the child from being interrupted during the next OnUpdate evaluation.

## [1.0.5] - 2024-11-08

### Changed
- Conditional Branch node will log a warning in `UNITY_EDITOR` if it has no child to run. This can be safely ignored if intentional.

### Fixed
- `Repeat While (RepeatWhileCondition.cs)` node will correctly start its conditions before checking them, and will not wait forever if its children finished on the same frame.
- Conditional Branch node will no longer throw exceptions if there is no child set.

## [1.0.4] - 2024-11-07

### Added

### Changed
- Updated AppUI dependency to `"2.0.0-pre.11`.
- Label displaying the graph description text removed from the subgraph node inspector.
- Increased variable link search view width a bit and increase the text's width in the search options.
- Changed the output location of IL2CPP link files so as to not pollute the Assets directory.
- If a NavMeshAgent is available, the Patrol node now uses its speed to feed the animator's speed parameter.
- Set `BehaviorGraphAgentEditor`'s `editorForChildClasses` to `true` in its `CustomEditor` property and added a call to `DrawPropertiesExcluding` in `OnInspectorGUI`.

### Fixed
- Fixed node wizard validation step not accepting other character sets than ASCII in variable names.
- Fixed Event Channel wizard not filling in types for variables with a matching name on the Blackboard when editing the event message.
- Fixed Event Channel wizard NodeUI preview not applying the correct margin styles on the message label.
- Removed additional "New" from the variable name when creating a new Event Channel or Enum variable on the Blackboard.
- Fixed an issue where the Blackboard would be reloaded when opening the asset searcher on a variable asset field.
- Fixed not assigned EventChannels not generating local instances by BehaviorGraphAgent when the BlackboardVariable was part of a BlackboardReference.
- Fixed blackboard variables added through the subgraph story representation having extra spaces between words.
- Deleting a Blackboard Asset will correctly remove references in other graphs and unset variable links that reference its variables.
- ComponentToComponentBlackboardVariableCaster now checks the source object type for polymorphic type before using GetComponent.
- Pressing Spacebar in the graph to open the search menu will no longer throw an exception if using InputSystem with no Mouse support.
- Navigate and Patrol actions will correctly check for null and not throw an exception due to incorrect use of ReferenceEquals.
- Fixed a runtime type construct serialization issue, where a types with multiple generic components were failing.
- Fixed FindClosestWithTagAction condition that was preventing the node to be executed when a null target was provided.
- Fixed PlayAudioAction nullreference when the pooling was attempting to release an AudioSource that has already been destroyed.
- Adds runtime serialization support to Switch and Patrol node.
- Fixed runtime serialization support for MathTypesCastBlackboardVariables.
- BlackboardVariables from added Blackboard Assets that can be cast to a field weren't shown as link options.
- Fixed an issue in Conditions causing linked variables blackboard asset prefix to be removed when using the graph.
- Fixed both Blackboard.SetVariableValue api's to allow for setting a variable via an object.
- Cooldown node will correctly initialize its wait time and won't block graph execution while waiting.
- Being unable to zoom in the graph after opening another graph with the Open button.

## [1.0.3] - 2024-10-14

### Added
- TriggerEvent now logs message when EventChannel is not assigned.
- Added MakeSceneActive option to LoadLevel node for Additive mode. 
- Added new UI visualization for Shared blackboard variables.

### Changed
- Improved logging details when an exception is raised about an invalid node transformer.
- Added logging on domain reload when any node BlackboardVariable is found that doesn't have the [SerializeReference] attribute.
- Added logging on domain reload when any Composite field of type Node is found that doesn't have the [SerializeReference] attribute.
- Added missing Switch node inspector.
- Fixed Shared Blackboard Variable causing a stack overflow in case it was referencing itself.
- Fixed invalid generation of Shared Blackboard Variable on the source runtime blackboard asset.
- Blackboard variable Exposed and Shared values can now be toggled on and off from the variable instead of right-clicking. 
- Changed variable renaming to trim out any leading or trailing whitespace. 
- The branch generation correction feature has been disabled for backend related issues.

### Fixed
- Fixed dynamic subgraph not initializing local event channels.
- Fixed dynamic subgraph overriding shared blackboard variables.
- Fixed static subgraphs not passing down same type component variables properly.
- Fixed audio resources being able to be passed into an audio clip link field when they shouldn't be.
- Fixed any component being able to be passed into any component link field instead of only when it's a subclass.
- Fixed cast variable types not working correctly in subgraphs.
- Fixed subgraph blackboard variables not marked as exposed showing in parent graph node inspector.
- Fixed a bug where StartOnEvent node (Restart mode) would only execute the first node of a sequence after restarting in case it was restarted by a TriggerEvent node on the same frame.
- Fixed an issue with node wizard preview not displaying when typed variable fields were added.
- Fixed Muse branch generation correction deleting the previously generated branch if a valid output failed to be generated.
- Fixed placeholders not generating properly with generative AI.
- Fixed top and bottom lines of edge where the end is above the start not being selectable.
- Fixed Blackboard editor not syncing with changes from graph when the graph default blackboard was being edited.
- Fixed nullref exception caused by renaming a type serialized in a Blackboard Variable.
- Fixed blackboard and graph editor toolbar height changing on smaller window sizes.
- The Node Inspector will correctly save values edited in its fields when clicking outside the node inspector after editing.
- Fixed null exceptions when casting a GameObject or Component variable but the source is null (IN-87592).

## [1.0.2] - 2024-09-20

### Added
- Tooltip to display the variable type name on story editor dropdowns.

### Changed
- Nodes that generate their own children (for example Switch and Flow nodes with named ports) will auto align their children better than before.
- Condition context menu will show "Inspect Script" instead of "Edit Script" when opened for a built-in condition.
- Switch Node: Removed the warnings about no connected child during operation to avoid spamming the log.
- Creating or renaming variables with a duplicate name is not allowed anymore within a Blackboard asset. 

### Fixed
- Text truncate issues on story editor dropdown elements.
- Blackboard variables from added blackboard assets can now be dragged and dropped onto fields in nodes.
- Fixed `CheckCollisionsInRadiusAction` failling to assign collided object if the BBV value was null.
- Nodes with inherited named children (i.e. Node connections) weren't showing the base class' nodes.
- Fixed Event Channel variables not appearing in link searches for non-event nodes.
- Fixed the Self variable not working in Dynamic Subgraphs.
- Fixed `GraphAssetProcessor` throwing exception when `AssetDatabase.SaveAssets` is called before the end of domain reload.
- Fixed dynamic subgraph not initializing local event channels.
- Fixes a bug where the StartOnEvent node could stop working if it received a message the same frame its subgraph ended.
- Fixed slowdown when adding lots of variables to a blackboard asset referenced in a graph.

## [1.0.1] - 2024-09-18

### Fixed
- Fixed an issue with the LinkField search menu popup element causing an exception on Event nodes.
- Set variable value didn't save the value if it was a cast blackboard variable.
- Text alignment for conditions in the inspector will now center correctly.
- Added missing space in variable comparison condition text.
- Fixed link fields being empty when duplicating a node that has a reference to an external blackboard variable.


## [1.0.0] - 2024-09-17

### Added
- PlayParticleSystem node can now assign the spawned object to InstantiatedObject.
- Conditions can now reference the GameObject associated with the graph.
- Updated the vector LinkField UI to new improved design.

### Changed
- Renamed `Unity Serialization Example` to `Runtime Serialization` and `Serialization` `MonoBehaviour` class to `SerializationExampleSceneController`.

### Fixed

- Switch enum child ports weren't interacting correctly after editing the enum while the graph is open.
- Fixed a hang when deleting runtime assets when blackboards are referenced.

## [1.0.0-pre.1] - 2024-09-13

### Added
- BlackboardVariable support for various Resource types.
- BlackboardVariables of type UnityEngine.Object can now have an embedded value in their field (previously needed to set it in the GameObject's inspector).
- Added new behaviors to StartOnEvent node (Default, Restart, Once).  
- Blackboard assets can now be created through the Project view Create menu.
- Added serialization/deserializatioon support for graph, blackboard and node values to JSON. Added demonstation sample.
- Allow using `TooltipAttribute` on BlackboardVariables to display a tooltip.
- Can now right click on a Blackboard Variable to copy its GUID.
- Can now right click on a Blackboard Variable to copy its GUID.
- Inspector will now display an inspection of the behavior graph, when nothing else is being selected.
- The Story button on the editor toolbar has been removed, and the graph's subgraph representation can now be edited through the Inspector.
- Transform Blackboard Variable type to the default blackboard options.
- Can add Blackboard references to graphs.
- Shared variables that are shared between instances of the same graph.
- Run Subgraph node combines now two nodes, a static Run Subgraph and the new Run Subgraph Dynamic, which can be used to run graphs dynamically on runtime.
- Subgraph variables can now be added on the Blackboard.
- Adds new built-in nodes
- Transform Blackboard Variable type to the default blackboard options.
- Can add Blackboard references to graphs.
- Shared variables that are shared between instances of the same graph.
- Run Subgraph node combines now two nodes, a static Run Subgraph and the new Run Subgraph Dynamic, which can be used to run graphs dynamically on runtime.
- Subgraph variables can now be added on the Blackboard.

### Changed

- AppUI dependency updated to 2.0.0-pre.6.
- Blackboard, Node Wizard and Condition Wizard now use a search window for variables.
- Removed variable icons.
- Creation wizards fields will be automatically focused when shown.
- Replaced icons for behavior graph agent and the graph asset with new designs.
- Updated node filenames and class names to have appropriate postfixes.
- Replaced all mouse events & usages to pointer events.
- Updated MoveToLocation and MoveToTarget logic for slowing down as target reached to use a distance from target instead of Min(Speed, Distance);
- Updated blackboard variable field assignment to support implicit casting between GameObject and components, components and GameObjects as well as components of different types.
- Moved existing built-in nodes to package namespace
- Updated built-in nodes categories
- Merged Repeat nodes into a single node.
- Merged Run in Parallel nodes into a single node.
- Updated all tutorial images to be JPEG intead of PNG for a significant memory saving.
- Start node repeat field is no longer a link field.

### Fixed

- Disabled inspector buttons for editing the definitions of built-in nodes.
- Inspector showing buttons for editing the definitions of built-in nodes.
- Fixed custom node definition editing not cleaning up existing properties.
- Crashes on Domain reload caused by OnAssetsSave().
- Performance when dragging nodes around a graph.
- Fixed newly created Conditions not being automatically added to the selected Conditional node.
- Fixed RepeatWhile node behaving like a do while.
- Conditions not getting copied properly when a conditional node was duplicated.
- Fixed navigation nodes sometimes stalling after being called successively.
- Fixed click events going through to elements underneath search menu items.
- Sequence insertion indicator didn't render at the correct width when dragging a node on top of a node then moving the mouse ontop of another node.
- Multiple instances of graphs being kept in memory when processed on save.
- Fixed pressing delete on an empty graph adding unnecessary command.
- Inheriting from another Node will correctly display the inherited Blackboard Variables.
- Fixed Graph Editor title blocking interaction with Open and Debug buttons.
- Crashes on Domain reload caused by OnAssetsSave().
- Fixed asset name in graph editor not updating when asset name changed on disc.
- Fixed newly created Conditions not being automatically added to the selected Conditional node.
- Fixed RepeatWhile node behaving like a do while.
- Conditions not getting copied properly when a conditional node was duplicated.
- Fixed navigation nodes sometimes stalling after being called successively.
- Fixed click events going through to elements underneath search menu items.
- Fixed UI styling issues on the Start On Event node. 
- Fixed warnings being logged unnecessarily in the BehaviorGraphAgent API's.

### Known Issues
- Clearing a node field, saving and reverting the asset in source control does not restore the field value.
- PlayParticleSystem internal pooling does not support runtime deserialization.

## [0.10.1] - 2024-06-14

### Fixed

- Enum fields in a condition don't save user's choice of enum value.

### Known Issues

- Graph Editor performance can be slow at the start of an action when there are a lot of nodes.
- Sometimes after dragging a node in and out of a sequence it won't be sequenceable and edges won't connect to it. Re-opening the asset seems to fix it.
- Undo/Redo often causes exceptions.
- Edge rendering has a bit of a snap when moving the lower node above the top node. Needs to be smoothed out.
- Dragging multiple nodes out of a sequence sometimes causes them to be on top of each other.
- Editor window does not react to asset name changes until the window is reopened.
- When creating a new node (i.e. Action, Modifier, Sequencing/Flow), the dialog does not immediately focus the “Name” input field, nor does it automatically focus the “Story” input field.
- Graph Asset gets serialized with minor changes even when no changes were made.

## [0.10.0] - 2024-06-13

### Added

- Documentation. Documentation!!! Rejoice!

### Fixed

- BlackboardVariable will only invoke its OnValueChanged callback if the value has changed. This also fixes situatiosn where the Variable Value Changed Condition was called when the same value was set.
- BehaviorGraphAgent is now public instead of internal when Netcode For GameObjects is included.
- Abort/Restart nodes inspector label is fixed to correctly say "Restarts if"/"Aborts if" depending on the node type.
- Dragging a node within a sequence won't move it if the insertion is where the node already is.
- Improve edge rendering when the end point is above the start point.
- Warnings with regard to UI Toolkit attributes in Unity 6.

### Changed

- Conditional nodes (Conditional Guard, Conditional Branch & Repeat While Condition) will now accept custom conditions, and multiple conditions can be added on the nodes.
- Reduced impact on build size by over ~100 MB by removing assets from Resource folders.
- Add Node window title will be "Add Node" instead of Root when at the root option.
- Dragging items into a sequence will sort them by order on the graph, not selection order.
- Rename the Node Inspector title from "Inspector" to "Node Inspector".
- Story text fields on Modifier and Sequencing node wizards are now optional.
- Added more documentation.

### Known Issues

- Sometimes after dragging a node in and out of a sequence it won't be sequenceable and edges won't connect to it. Re-opening the asset seems to fix it.

## [0.9.1] - 2024-05-24

### Fixed

- Move an Editor only method call that leaked into the runtime in #if UNITY_EDITOR.

## [0.9.0] - 2024-05-23

### Added

- Added new Abort and Restart nodes for interrupting branch execution.
- Condition wizard that enables custom condition creation for Abort and Restart nodes, which can be found through the 'Create new Condition' option when assigning a condition through the inspector.

### Fixed

- Fixed null reference errors when ending nodes with no child assigned.
- Improved the visual appearance of nodes by removing extra margins on LinkFields.
- Changes to subgraph assets will now trigger referencing graph assets to be rebuilt.
- Domain reloads will no longer create duplicate runtime assets.
- Deleting custom node scripts will no longer cause the graph to be corrupt.
- Fixed an issue where dialog windows would collapse in size when the graph editor window was resized.
- Fixed an issue where setting the Start node Repeat value from the inspector did not update on the graph node.

### Changed

- Nodes in a graph which had their scripts deleted will be replaced with placeholder nodes. These nodes are skipped at runtime.
- Moved the BehaviorGraphAgent component to AI/Muse Behavior Agent.
- Added an icon to the BehaviorGraphAgent component.
- Added info icon to Placeholder Toast message.
- Moved Placeholder Toast to the top.
- Moved the close button to be always on the right side of dialog elements.
- Link button now tints on mouse hover, showing you can click it.

## [0.8.0] - 2024-05-02

### Fixed

- Fixed WebGL build failures due to compilation errors stemming from reference to the Unity.Muse.Chat namespace.
- Link fields for enum types are now preserved on IL2CPP platforms.
- Enums should no longer be populated with the wrong members. Old enum variables with the error should be deleted and re-added.

### Changed

- Nodes no longer need to end their child nodes in OnEnd(). The OnEnd method will serve purely for managing the wrap-up of the node's execution.

## [0.7.1] - 2024-04-05

### Added

- Debug nodes for logging variable values to the console.
- Added Muse dropdown to the graph editor toolbar.
- Using Generative AI features now consumes Muse points.
- Added 'R' hotkey to frame to the root node.

### Fixed

- Applied correct minimum sizes for the Blackboard and Inspector floating panel content.
- Fixed flex style values on branch correction widget panel elements.
- Fixed cast exceptions upon aligning nodes while edges are selected.
- Ensured that list variable item input fields are full width on Blackboard.
- Fixed mismatched field type warnings on conditional nodes and set variable value nodes.
- Event nodes will now correctly update which channel they are listening on when the channel variable is reassigned.

### Changed

- Moved OnValueChanged callback to base BlackboardVariable class.
- Generative AI features now use the Muse Chat backend.

### Changed

- Disabled behavior graph agents are no longer selectable in the debug menu.

## [0.6.4] - 2024-03-22

### Fixed

- An issue preventing certain nodes from being added to a sequence.
- Enum creation wizard wouldn't create the fields correctly.
- Node creation wizard stopped updating the preview node.
- Debug warning when assigning a new type of enum to a switch node.

### Known Issues

- Enums which share a name with a class from another assembly don't always show correctly in the Blackboard.
- An exception relating to SetName after domain reload.

## [0.6.3] - 2024-03-20

### Fixed

- Correctly modify event node fields after drag and drop an event channel blackboard variable.

## [0.6.2] - 2024-03-20

### Added

- Allow drag and drop of variables from the Blackboard onto compatible Node fields (Unity versions 2023.2+ only).

### Fixed

- MoveTo actions: Improved stopping distance to take colliders into account and avoid situations navigation can't end.

### Changed

- Selecting a new node to be added after pressing the space key will add the node to the end of a sequence if one is selected.

## [0.6.1] - 2024-03-18

### Fixed

- SearchView: Returning in navigation will correctly select the previous node you entered.
- Move To Target node should correctly update the Nav Mesh's destination if the target moved.
- Fixed an exception related to event nodes.

## [0.6.0] - 2024-03-15

### Added

- Added PlaceholderActionNodes
- Added creating Placeholder nodes for missing actions when generating a branch with AI
- Added PlaceholderActionNodes widget and navigation
- List variable support.
- Added a OnValueChanged callback for Blackboard variable value changes.
- When connecting an edge, escape key will cancel the edge drag.
- Node creation wizards will now show Vector4 in the dropdowns options.
- Behavior Agent Inspector: Right click context menu to reset variables with an override.
- Allow drag and drop asset to the Muse Behavior Graph field in the inspector.
- Allow drag and drop asset on a GameObject in the hierarchy, adding the BehaviorAgent if needed.
- Allow drag and drop asset on a GameObject inspector space, adding the BehaviorAgent if needed.

### Fixed

- Sticky node changes should register for serialisation correctly.
- Dragging a node onto another to create a sequence should correctly maintain output connections.
- Dragging a node or sequence onto an existing sequence, connections should be maintained where possible.
- Search window keyboard controls and arrow navigation should work as expected.
- Blackboard variables on agent that haven't been overwritten should update to new value.
- After deleting an edge, undo correctly restores the link between the UI and asset data, preventing phantom edges.
- Fixed LinkFields not updating their fields values reliably on undo-redo.

### Changed

- Area selection will now select edges.
- Duplicating and pasting nodes will select the duplicated nodes.
- Overwritten variables on the agent will show an '(Override)' label.

## [0.5.11] - 2024-03-06

### Fixed

- Support with latest AppUI package (1.0.2).

## [0.5.10] - 2024-03-05

### Added

- Added a tooltip for node input fields which displays the field variable type.
- Added a cooldown node.

### Fixed

- Fixed the placement of duplicated nodes. When using the duplicate hotkey (CTRL/CMD + D), nodes are now correctly placed at mouse position.
- Fixed assignment of the graph owner object when switching graphs at runtime.
- Fixed stripping of GUID properties in WebGL builds.
- Fixed stripping node types in IL2CPP builds.
- Fixed access to generative features for Muse subscribers.
- Modifying fields in the blackboard now triggers the asset to save.
- Fixed succeeder nodes indefinitely waiting after a child node has completed.
- Fixed modifier nodes not resetting child branch statuses.
- Patrol no longer gets stuck when there are only 2 points.
- Including Netcode for GameObjects caused errors.

### Changed

- Reduced the visual size of sequences.
- Increased the hit box for sequences.
- Graphs now add an instance of a newly created type after generation.

## [0.5.8] - 2024-01-10

### Fixed

- Fixed the alignment of the wizard panel title text.
- Fixed the Vector field widths on inspector.
- Fixed node generation issues when editing the definition of a built-in node.
- Removed unnecessary user prompts when editing a node with hidden variables that are not included in the node story.
- Fixed incorrect method access in BehaviorGraphAgent, causing Netcode for GameObjects errors.

## [0.5.7] - 2023-12-08

### Fixed

- Max serialisation depth reached errors.
- Vector styling in the inspector and nodes UI inside the graph.
- Color and Vector fields not showing correctly in the GameObject inspector.

## [0.5.6] - 2023-12-06

### Fixed

- Fixed VariableModel and TypedVariableModel related errors when building with IL2CPP.
- Fixed exceptions in collision nodes.
- UI: EnumLinkField text wrapping shouldn't happen anymore.
- Error with Input System for Unity 2021.
- Correctly diffrentiate InputSystem deprecated APIs across Unity versions.
- LinkField label color wasn't right in some nodes.
- Various Node UI fixes.
- Certain nodes OnEnd weren't called.

### Changed

- Collision Nodes: If the tag is empty, detect all collisions.
- Removed scroll bars from the graph canvas.

## [0.5.5] - 2023-11-16

### Added

- Minimum window size for Muse Behavior Window.
- Move collision nodes from Sample to Package

### Fixed

- Fixed UnityEditor call preventing compilation.
- Fixed certain type assignments in subgraph fields.
- Missing meta file warnings.
- Error with Input System for Unity 2021.

### Changed

- Updated documentation.

## [0.5.4] - 2023-11-16

### Fixed

- Error after installing the package caused by the Domain Reload not finding test dependencies.

## [0.5.3] - 2023-11-15

### Added

- Orange UI stripe on conditional action nodes.
- Debug status for active subgraphs.
- Allow opening the authoring graph by double clicking the runtime sub-asset.
- Add inspectors for Conditional Guard, Branch and Repeat While nodes.

### Fixed

- Dependency on unit tests caused an exception on some versions of Unity.
- BlacboardEnum attribute was internal, causing an error when used.
- Fix loss of local value on Set Variable Value node.
- An issue with subgraphs fields not allowing you to set certain types.
- Incorrect null check in FindClosestWithTagAction and FindObjectWithTagAction, preventing them from functioning.

## [0.5.2] - 2023-11-14

### Added

- New UI designs implemented.
- Hotkeys: A and Shift+A to align selected nodess' immediate children (A) and all nodes under selected nodes (Shift+A).
- Suggested variable types in node wizards.
- Variable equality evaluator node types.
- Auto-linking of variables by name and recent match.
- Switch serialization of graph instances to use JSON to support 2021 LTS and 2022 LTS.
