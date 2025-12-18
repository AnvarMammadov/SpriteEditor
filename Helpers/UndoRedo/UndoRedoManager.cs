using System;
using System.Collections.Generic;
using System.Linq;

namespace SpriteEditor.Helpers.UndoRedo
{
    /// <summary>
    /// Manages undo/redo stacks for the entire application
    /// </summary>
    public class UndoRedoManager
    {
        private readonly Stack<IUndoableCommand> _undoStack = new();
        private readonly Stack<IUndoableCommand> _redoStack = new();
        private readonly int _maxStackSize = 100; // Prevent memory issues

        // Events for UI updates
        public event EventHandler StacksChanged;

        // Singleton pattern
        private static UndoRedoManager _instance;
        public static UndoRedoManager Instance => _instance ??= new UndoRedoManager();

        private UndoRedoManager() { }

        /// <summary>
        /// Execute a command and add it to undo stack
        /// </summary>
        public void ExecuteCommand(IUndoableCommand command)
        {
            if (command == null) return;

            try
            {
                // Execute the command
                command.Execute();

                // Try to merge with last command (optimization for continuous operations)
                if (_undoStack.Count > 0 && command.CanMergeWith(_undoStack.Peek()))
                {
                    var lastCommand = _undoStack.Peek();
                    lastCommand.MergeWith(command);
                }
                else
                {
                    // Add to undo stack
                    _undoStack.Push(command);

                    // Limit stack size
                    if (_undoStack.Count > _maxStackSize)
                    {
                        var list = _undoStack.ToList();
                        list.RemoveAt(list.Count - 1); // Remove oldest
                        _undoStack.Clear();
                        list.Reverse();
                        foreach (var cmd in list)
                            _undoStack.Push(cmd);
                    }
                }

                // Clear redo stack (new action invalidates redo history)
                _redoStack.Clear();

                OnStacksChanged();
            }
            catch (Exception ex)
            {
                GlobalErrorHandler.LogError(ex, "UndoRedoManager.ExecuteCommand");
                throw;
            }
        }

        /// <summary>
        /// Undo the last command
        /// </summary>
        public void Undo()
        {
            if (!CanUndo) return;

            try
            {
                var command = _undoStack.Pop();
                command.Undo();
                _redoStack.Push(command);

                OnStacksChanged();
            }
            catch (Exception ex)
            {
                GlobalErrorHandler.LogError(ex, "UndoRedoManager.Undo");
                throw;
            }
        }

        /// <summary>
        /// Redo the last undone command
        /// </summary>
        public void Redo()
        {
            if (!CanRedo) return;

            try
            {
                var command = _redoStack.Pop();
                command.Execute();
                _undoStack.Push(command);

                OnStacksChanged();
            }
            catch (Exception ex)
            {
                GlobalErrorHandler.LogError(ex, "UndoRedoManager.Redo");
                throw;
            }
        }

        /// <summary>
        /// Clear all undo/redo history
        /// </summary>
        public void Clear()
        {
            _undoStack.Clear();
            _redoStack.Clear();
            OnStacksChanged();
        }

        /// <summary>
        /// Get description of next undo command
        /// </summary>
        public string GetUndoDescription()
        {
            return CanUndo ? _undoStack.Peek().Description : "Nothing to undo";
        }

        /// <summary>
        /// Get description of next redo command
        /// </summary>
        public string GetRedoDescription()
        {
            return CanRedo ? _redoStack.Peek().Description : "Nothing to redo";
        }

        /// <summary>
        /// Can we undo?
        /// </summary>
        public bool CanUndo => _undoStack.Count > 0;

        /// <summary>
        /// Can we redo?
        /// </summary>
        public bool CanRedo => _redoStack.Count > 0;

        /// <summary>
        /// Get undo stack count (for UI display)
        /// </summary>
        public int UndoCount => _undoStack.Count;

        /// <summary>
        /// Get redo stack count (for UI display)
        /// </summary>
        public int RedoCount => _redoStack.Count;

        private void OnStacksChanged()
        {
            StacksChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}

