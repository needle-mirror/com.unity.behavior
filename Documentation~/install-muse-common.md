---
uid: muse-common
---

# Install Muse Common with the Package Manager

From Unity Behavior version `1.0` onward, the Muse Common package is no longer included automatically as a dependency. Consequently, Unity Behavior disables the generative artificial intelligence (AI) features by default and doesn't compile them. To enable these advanced features and maintain control over the additional dependencies in your project, install Muse Common manually.

To install Muse Common, follow these steps:

1. Launch the Unity Editor and open the project where you want to install Muse Common.

2. In the Unity Editor, select **Window** > **Package Manager**.

   The Package Manager window opens.
3. From the **Add** menu, select **Add package by name…**.
4. In the **Name** field, enter `com.unity.muse.common`.
5. (Optional) In the **Version (optional)** field, enter `2.0.5` or a later version.

   The minimal supported version of Muse Common is `2.0.5`.

## Additional resources

 * [Install Unity Behavior with the Package Manager](install-behavior.md)
 * [Create a behavior graph](create-behavior-graph.md)