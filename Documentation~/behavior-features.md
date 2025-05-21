---
uid: behavior-features
---

# Unity Behavior key features

Unity Behavior accelerates behavior authoring with a visual workflow, runtime integration, and modular architecture.

The following features set Unity Behavior apart from other Unity solutions:

* **Human-readable graphs**: Behavior graphs read from top to bottom and use plain-language nodes that describe gameplay actions. This makes them easier to read, debug, and iterate upon.

* **Integration with C#**: Unity Behavior complements traditional C# workflows. You can integrate visual behaviors with existing codebases, trigger behavior graphs from scripts, and create custom nodes using familiar Unity patterns. Custom nodes follow a lifecycle model similar to MonoBehaviour with **OnStart**, **OnUpdate**, and **OnEnd** methods.

* **Pre-built node library**: Unity Behavior includes a collection of built-in nodes that cover common behavior patterns, such as movement, detection, conditions, decision-making, and more. Use these nodes to build behaviors quickly without starting from scratch.

* **Hierarchical and reusable design**: Unity Behavior supports subgraphs and modular graph composition. Break down complex behaviors into smaller, manageable graphs that you can reuse across different characters, levels, or scenarios.

* **Real-time debugging**: During Play mode, you can visualize an agent's behavior flow live in the editor. This real-time feedback speeds up testing and reduces iteration time.

* **Variable and Blackboard support**: Store and manage data across graphs (and scripts) with Blackboards and variables. They let you pass information between nodes and behavior graphs.

* **Event-driven behavior**: Trigger behavior changes with events, custom event nodes, or script-based triggers. This helps behavior graphs to react dynamically to in-game events or state changes.

## Intended audience

Unity Behavior supports a range of roles across game development teams. Its visual workflow and flexible integration with C# make it useful across disciplines.

* **Game designers**: You can create artificial intelligence (AI) behaviors and interactive elements without writing code. The readable graph structure and plain language nodes make it accessible even without programming experience.

* **Gameplay programmers**: Unity Behavior removes boilerplate code and provides a structured framework to implement game behaviors. You can create custom nodes in C# and visually compose complex logic using graphs.

* **Cross-disciplinary teams**: Unity Behavior creates a shared language between designers and engineers. Designers can prototype behaviors and clearly express gameplay intent through graphs, while programmers can build underlying systems and reusable components. This collaboration speeds up iteration and improves team alignment.

## Use case examples

Unity Behavior is ideal for the following types of gameplay systems that involve dynamic behaviors, decision-making, and interactive logic:

* NPC behavior systems, such as enemies, companions, crowds
* Dialog and conversation systems
* Quests and mission logic
* Environmental interactions and reactions
* Cinematic sequences and timed events
* Game state management
* In-game tutorial and guidance systems

## Additional resources

* [How Unity Behavior compares to other Unity solutions](behavior-differ.md)
* [Behavior graphs](behavior-graph.md)
* [Behavior graph node types](node-types.md)
* [Unity Behavior user interface](user-interface.md)