using System;
using Unity.Properties;
using UnityEngine;

namespace Unity.Behavior
{
    [Serializable]
    [NodeModelInfo(typeof(StartOnEvent))]
    internal class StartOnEventModel : EventNodeModel
    {
        public const string k_RestartOnNewMessageNodeUITitleName = "(Restart)";
        public const string k_TriggerOnceNodeUITitleName = "(Once)";
        public const string k_TriggerModeFieldName = "Mode";
        public const string k_TriggerModeTooltips =
            "Select the event trigger behavior." +
            "\n- \"Default\": Processes a message only if the node is idle (no child node is running). " +
                "Ignores any messages that arrive while busy." +
            "\n- \"Restart\": When a message is received, stops all running child nodes and restarts execution from this node." +
            "\n- \"Once\": Processes only the first message received, then permanently stops listening to the event channel." +
            "\n- \"Queue\": The node stores all received messages in a queue. When the node becomes idle " +
                "(no child node is running), it processes one message from the queue, " +
                "then waits until idle again before processing the next queued message.";

        public override bool IsDuplicatable => true;
        public override bool IsSequenceable => false;
        public override bool IsRoot => true;

        public override bool HasDefaultInputPort => false;
        public override int MaxInputsAccepted => 0;

        public StartOnEvent.TriggerBehavior TriggerBehavior;

        public StartOnEventModel(NodeInfo nodeInfo) : base(nodeInfo) { }

        protected StartOnEventModel(StartOnEventModel original, BehaviorAuthoringGraph asset)  : base(original, asset)  { }
    }
}