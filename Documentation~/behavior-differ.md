---
uid: behavior-differ
---

# How Unity Behavior compares to other Unity solutions

Unity provides different graph-based tools, each designed for different purposes. This section describes how Unity Behavior compares to other solutions, such as Visual Scripting and Graph Toolkit.

## Unity Behavior and Unity Visual Scripting

The following table describes the key differences between [Unity Visual Scripting](https://unity.com/features/unity-visual-scripting) and Unity Behavior to help you choose the right tool based on your project needs.

| Aspect | Unity Visual Scripting | Unity Behavior |
| ------ | ---------------------- | -------------- |
| Purpose | Visual alternative to C# for implementing general game logic. | High-level tool for defining what an entity should do next. |
| Abstraction level | Low-level: focuses on how to implement logic, step by step. | High-level: focuses on behavior outcomes like success, failure, or waiting. |
| Design model	| Based on programming constructs (for example, variables, loops, and conditionals). | Based on behavior tasks and decision-making states. |
| Example question | `How do I implement this functionality?` | `What should this object or character do next?` |
| Typical applications | Broad logic including UI flow, gameplay systems, or mathematical operations. | AI behavior, interactions, contextual event handling, character decisions. |

## Unity Behavior and Graph Toolkit

The following table describes how Unity Behavior compares to the Graph Toolkit.

| Aspect | Graph Toolkit | Unity Behavior |
| ------ | ---------------------- | -------------- |
| Purpose | A foundational framework to create custom graph-based tools and editors. | A complete, ready-to-use package for authoring gameplay behaviors. |
| Audience | Developers that build custom solutions and tools. | Designers and programmers that need a pre-built system to create behaviors. |
| Customization | Highly customizable: requires coding to implement specific functionality. | Pre-defined nodes and workflows ready for immediate use. Customization limited to nodes and variables.|
| Use Case | Ideal for custom animation graphs or toolchains. | Ideal for implementing AI, event systems, and interactive behaviors without building a framework. |

## Additional resources

* [Create and manage variables and Blackboards](blackboard-variables.md)
* [Create a behavior graph](create-behavior-graph.md)
* [Save and load running graph state](serialization.md)