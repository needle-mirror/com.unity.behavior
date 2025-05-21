using System;
using System.Collections.Generic;
using UnityEngine;

namespace Unity.Behavior.GraphFramework
{
    internal class Dispatcher
    {
        private readonly Dictionary<Type, List<BaseCommandHandler>> m_CommandTypeToHandlers = new();
        private readonly Queue<Command> m_DispatchQueue = new();
        private readonly IDispatcherContext m_DispatcherContext;

        public Dispatcher(IDispatcherContext context)
        {
            m_DispatcherContext = context;
        }

        public void RegisterHandler<CommandType, HandlerType>() 
            where CommandType : Command
            where HandlerType : CommandHandler<CommandType>, new()
        {
            RegisterHandler<CommandType, HandlerType>(new HandlerType());
        }

        public void RegisterHandler<CommandType, HandlerType>(HandlerType handler) 
            where CommandType : Command
            where HandlerType : CommandHandler<CommandType>                                                               
        {
            handler.DispatcherContext = m_DispatcherContext;
            if (m_CommandTypeToHandlers.TryGetValue(typeof(CommandType), out List<BaseCommandHandler> commandHandlers)) 
            {
                commandHandlers.Add(handler);
            }
            else
            {
                commandHandlers = new List<BaseCommandHandler> { handler };
                m_CommandTypeToHandlers.Add(typeof(CommandType), commandHandlers);
            }
        }

        public void UnregisterHandler<CommandType, HandlerType>()
            where CommandType : Command
            where HandlerType : CommandHandler<CommandType>                     
        {
            if (m_CommandTypeToHandlers.TryGetValue(typeof(CommandType), out List<BaseCommandHandler> commandHandlers)) 
            {
                commandHandlers.RemoveAll(handler => handler is HandlerType);
            }
        }

        public void Dispatch(Command command)
        {
            m_DispatchQueue.Enqueue(command);
        }

        public void DispatchImmediate(Command command, bool setHasOutstandingChanges = true)
        {
            Type commandType = command.GetType();
            if (!m_CommandTypeToHandlers.TryGetValue(commandType, out List<BaseCommandHandler> commandHandlers))
            {
                Debug.LogWarning("No registered command handler for command type: " + commandType.Name);
                return;
            }

#if UNITY_EDITOR
            if (command.MarkUndo)
            {
                DispatchAndProcessUndoableCommand(command, setHasOutstandingChanges, commandHandlers);
            }
            else
            {
                if (command.SetAssetDirty)
                {
                    // We always dirty graph asset when available as nested blackboard asset is depending on it to be saved.
                    if (m_DispatcherContext.GraphAsset != null)
                    {
                        m_DispatcherContext.GraphAsset.SetAssetDirty(setHasOutstandingChanges);
                    }
                    if (command is IBlackboardAssetCommand && m_DispatcherContext.BlackboardAsset != null)
                    {
                        m_DispatcherContext.BlackboardAsset.SetAssetDirty();
                    }
                }

                foreach (BaseCommandHandler commandHandler in commandHandlers)
                {
                    if (commandHandler.Process(command))
                    {
                        break;
                    }
                }
            }
#else
            foreach (BaseCommandHandler commandHandler in commandHandlers)
            {
                // Command is process and asset might be dirty here
                if (commandHandler.Process(command))
                {
                    break;
                }
            }
#endif
        }

        public void Tick()
        {
            while (m_DispatchQueue.TryDequeue(out Command command))
            {
                DispatchImmediate(command);
            }
        }

        public void ClearDispatchQueue()
        {
            m_DispatchQueue.Clear();
        }

#if UNITY_EDITOR
        private bool m_IsProcessingCommandChain = false;
        private int m_CurrentCommandChainGroup = -1;
        private List<Command> m_ActiveCommandChain = new();

        /// <summary>
        /// Dispatches a command with proper undo/redo support, handling both standalone commands and command chains.
        /// </summary>
        /// <remarks>
        /// Commands can trigger other commands during processing (like DeleteVariable triggering SetNodeLinkedVariable).
        /// This implementation handles these nested command chains by grouping them into a single undo operation.
        /// Modifying this approach risks breaking undo/redo consistency and creating inconsistency in the different editor view.
        /// </remarks>
        private void DispatchAndProcessUndoableCommand(Command command, bool setHasOutstandingChanges, List<BaseCommandHandler> commandHandlers)
        {
            string assetName = command is IBlackboardAssetCommand ?
                                (m_DispatcherContext.BlackboardAsset ? $" ({UnityEditor.AssetDatabase.GetAssetPath(m_DispatcherContext.BlackboardAsset)})" : string.Empty) :
                                (m_DispatcherContext.GraphAsset ? $" ({UnityEditor.AssetDatabase.GetAssetPath(m_DispatcherContext.GraphAsset)})" : string.Empty);
            string outstandingString = setHasOutstandingChanges ? "(outstanding) " : string.Empty;
            string commandName = command.GetType().Name.Replace("Command", "") + $"{outstandingString}" + assetName;
            
            // Start a new undo group only if this is the first command in a chain
            if (m_IsProcessingCommandChain == false)
            {
                UnityEditor.Undo.IncrementCurrentGroup();
                UnityEditor.Undo.SetCurrentGroupName(commandName);
                m_CurrentCommandChainGroup = UnityEditor.Undo.GetCurrentGroup();
                m_IsProcessingCommandChain = true;
            }

            m_ActiveCommandChain.Add(command);

            // Note that we always dirty graph asset even when the command affect blackboard.
            // This is because in order to properly undo embedded blackboard asset (which is nested in the graph asset)
            // we need to also register the super-graph asset state with the Undo API.
            // This is not going to do anything when working on standalone Blackboard Asset,
            // as the dispatcher won't have a GraphAsset assigned.
            if (m_DispatcherContext.GraphAsset != null)
            {
                m_DispatcherContext.GraphAsset.MarkUndo(commandName, setHasOutstandingChanges);
            }
            if (command is IBlackboardAssetCommand && m_DispatcherContext.BlackboardAsset != null)
            {
                m_DispatcherContext.BlackboardAsset.MarkUndo(commandName);
            }

            foreach (BaseCommandHandler commandHandler in commandHandlers)
            {
                if (commandHandler.Process(command))
                {
                    break;
                }
            }

            // If we're back at the root command of the chain (the command that started it all),
            // finalize the undo group and reset the command chain state
            if (m_IsProcessingCommandChain && m_ActiveCommandChain[0] == command)
            {
                if (m_CurrentCommandChainGroup != -1)
                {
                    if (m_ActiveCommandChain.Count > 1)
                    {
                        string mainCommand = commandName + $"(+ {m_ActiveCommandChain.Count - 1} action(s))";
                        UnityEditor.Undo.SetCurrentGroupName(mainCommand);
                    }

                    UnityEditor.Undo.CollapseUndoOperations(m_CurrentCommandChainGroup);
                    m_CurrentCommandChainGroup = -1;
                }

                m_IsProcessingCommandChain = false;
                m_ActiveCommandChain.Clear();
            }
        }
#endif
    }
}