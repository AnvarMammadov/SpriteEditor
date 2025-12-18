using System;
using SkiaSharp;
using SpriteEditor.Helpers.UndoRedo.Commands;
using SpriteEditor.ViewModels;

namespace SpriteEditor.Helpers.UndoRedo
{
    /// <summary>
    /// Extension methods to integrate Undo/Redo into RiggingViewModel
    /// </summary>
    public static class RiggingViewModelExtensions
    {
        /// <summary>
        /// Add joint with undo support
        /// </summary>
        public static void AddJointWithUndo(this RiggingViewModel vm, JointModel joint)
        {
            var command = new AddJointCommand(vm.Joints, joint, joint.Parent);
            UndoRedoManager.Instance.ExecuteCommand(command);
        }

        /// <summary>
        /// Delete joint with undo support
        /// </summary>
        public static void DeleteJointWithUndo(this RiggingViewModel vm, JointModel joint)
        {
            var command = new DeleteJointCommand(vm.Joints, joint);
            UndoRedoManager.Instance.ExecuteCommand(command);
        }

        /// <summary>
        /// Move joint with undo support (use this when drag ends)
        /// </summary>
        public static void MoveJointWithUndo(this RiggingViewModel vm, JointModel joint, SKPoint oldPosition, SKPoint newPosition)
        {
            var command = new MoveJointCommand(joint, oldPosition, newPosition);
            UndoRedoManager.Instance.ExecuteCommand(command);
        }

        /// <summary>
        /// Add vertex with undo support
        /// </summary>
        public static void AddVertexWithUndo(this RiggingViewModel vm, VertexModel vertex)
        {
            var command = new AddVertexCommand(vm.Vertices, vertex);
            UndoRedoManager.Instance.ExecuteCommand(command);
        }

        /// <summary>
        /// Delete vertex with undo support
        /// </summary>
        public static void DeleteVertexWithUndo(this RiggingViewModel vm, VertexModel vertex)
        {
            var command = new DeleteVertexCommand(vm.Vertices, vertex);
            UndoRedoManager.Instance.ExecuteCommand(command);
        }

        /// <summary>
        /// Move vertex with undo support (use this when drag ends)
        /// </summary>
        public static void MoveVertexWithUndo(this RiggingViewModel vm, VertexModel vertex, SKPoint oldPosition, SKPoint newPosition)
        {
            var command = new MoveVertexCommand(vertex, oldPosition, newPosition);
            UndoRedoManager.Instance.ExecuteCommand(command);
        }

        /// <summary>
        /// Add triangle with undo support
        /// </summary>
        public static void AddTriangleWithUndo(this RiggingViewModel vm, TriangleModel triangle)
        {
            var command = new AddTriangleCommand(vm.Triangles, triangle);
            UndoRedoManager.Instance.ExecuteCommand(command);
        }

        /// <summary>
        /// Delete triangle with undo support
        /// </summary>
        public static void DeleteTriangleWithUndo(this RiggingViewModel vm, TriangleModel triangle)
        {
            var command = new DeleteTriangleCommand(vm.Triangles, triangle);
            UndoRedoManager.Instance.ExecuteCommand(command);
        }
    }
}

