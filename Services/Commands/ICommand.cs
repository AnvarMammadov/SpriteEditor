using System;

namespace SpriteEditor.Services.Commands
{
    /// <summary>
    /// Base interface for all undoable commands in the application.
    /// Implements the Command Pattern for undo/redo functionality.
    /// </summary>
    public interface ICommand
    {
        /// <summary>
        /// Executes the command (performs the action).
        /// </summary>
        void Execute();

        /// <summary>
        /// Undoes the command (reverts the action).
        /// </summary>
        void Undo();

        /// <summary>
        /// Human-readable description of the command for UI display.
        /// Example: "Add Joint 'LeftShoulder'"
        /// </summary>
        string Description { get; }

        /// <summary>
        /// Timestamp when the command was created.
        /// Useful for command history tracking and debugging.
        /// </summary>
        DateTime Timestamp { get; }
    }
}
