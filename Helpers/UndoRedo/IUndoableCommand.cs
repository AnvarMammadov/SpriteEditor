using System;

namespace SpriteEditor.Helpers.UndoRedo
{
    /// <summary>
    /// Interface for all undoable/redoable commands
    /// </summary>
    public interface IUndoableCommand
    {
        /// <summary>
        /// Execute the command (do the action)
        /// </summary>
        void Execute();

        /// <summary>
        /// Undo the command (reverse the action)
        /// </summary>
        void Undo();

        /// <summary>
        /// Description of the command (for UI display)
        /// </summary>
        string Description { get; }

        /// <summary>
        /// Whether this command can be merged with another (for optimization)
        /// </summary>
        bool CanMergeWith(IUndoableCommand other);

        /// <summary>
        /// Merge this command with another (for batch operations)
        /// </summary>
        void MergeWith(IUndoableCommand other);
    }
}

