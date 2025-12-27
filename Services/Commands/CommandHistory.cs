using System;
using System.Collections.Generic;
using System.Linq;

namespace SpriteEditor.Services.Commands
{
    /// <summary>
    /// Manages the undo/redo command history.
    /// Provides centralized state management for all undoable operations.
    /// </summary>
    public class CommandHistory
    {
        private readonly Stack<ICommand> _undoStack = new();
        private readonly Stack<ICommand> _redoStack = new();
        private readonly int _maxHistorySize;

        /// <summary>
        /// Event raised when the undo/redo state changes.
        /// Useful for updating UI button states.
        /// </summary>
        public event EventHandler StateChanged;

        public CommandHistory(int maxHistorySize = 100)
        {
            _maxHistorySize = maxHistorySize;
        }

        /// <summary>
        /// Executes a command and adds it to the undo stack.
        /// Clears the redo stack as new actions invalidate redo history.
        /// </summary>
        public void ExecuteCommand(ICommand command)
        {
            if (command == null)
                throw new ArgumentNullException(nameof(command));

            command.Execute();
            _undoStack.Push(command);

            // New action clears redo history
            _redoStack.Clear();

            // Limit history size to prevent memory issues
            if (_undoStack.Count > _maxHistorySize)
            {
                // Remove oldest command
                var temp = new Stack<ICommand>(_undoStack.Reverse().Skip(1).Reverse());
                _undoStack.Clear();
                foreach (var cmd in temp)
                    _undoStack.Push(cmd);
            }

            OnStateChanged();
        }

        /// <summary>
        /// Undoes the most recent command.
        /// </summary>
        public void Undo()
        {
            if (!CanUndo)
                return;

            var command = _undoStack.Pop();
            command.Undo();
            _redoStack.Push(command);

            OnStateChanged();
        }

        /// <summary>
        /// Redoes the most recently undone command.
        /// </summary>
        public void Redo()
        {
            if (!CanRedo)
                return;

            var command = _redoStack.Pop();
            command.Execute();
            _undoStack.Push(command);

            OnStateChanged();
        }

        /// <summary>
        /// Clears all command history.
        /// </summary>
        public void Clear()
        {
            _undoStack.Clear();
            _redoStack.Clear();
            OnStateChanged();
        }

        /// <summary>
        /// Gets whether undo is available.
        /// </summary>
        public bool CanUndo => _undoStack.Count > 0;

        /// <summary>
        /// Gets whether redo is available.
        /// </summary>
        public bool CanRedo => _redoStack.Count > 0;

        /// <summary>
        /// Gets the description of the next undo command, or null if none.
        /// </summary>
        public string NextUndoDescription => CanUndo ? _undoStack.Peek().Description : null;

        /// <summary>
        /// Gets the description of the next redo command, or null if none.
        /// </summary>
        public string NextRedoDescription => CanRedo ? _redoStack.Peek().Description : null;

        /// <summary>
        /// Gets the number of commands in the undo stack.
        /// </summary>
        public int UndoCount => _undoStack.Count;

        /// <summary>
        /// Gets the number of commands in the redo stack.
        /// </summary>
        public int RedoCount => _redoStack.Count;

        /// <summary>
        /// Gets the most recent command executed (for status display).
        /// </summary>
        public ICommand LastExecutedCommand => CanUndo ? _undoStack.Peek() : null;

        protected virtual void OnStateChanged()
        {
            StateChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}
