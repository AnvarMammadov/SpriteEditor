using System;
using System.Collections.ObjectModel;
using SkiaSharp;
using SpriteEditor.ViewModels;

namespace SpriteEditor.Helpers.UndoRedo.Commands
{
    // ============================================================================
    // BASE COMMAND CLASS
    // ============================================================================
    
    public abstract class BaseUndoableCommand : IUndoableCommand
    {
        public abstract void Execute();
        public abstract void Undo();
        public abstract string Description { get; }

        public virtual bool CanMergeWith(IUndoableCommand other) => false;
        public virtual void MergeWith(IUndoableCommand other) { }
    }

    // ============================================================================
    // ADD JOINT COMMAND
    // ============================================================================
    
    public class AddJointCommand : BaseUndoableCommand
    {
        private readonly ObservableCollection<JointModel> _joints;
        private readonly JointModel _joint;
        private readonly JointModel _parent;

        public AddJointCommand(ObservableCollection<JointModel> joints, JointModel joint, JointModel parent)
        {
            _joints = joints;
            _joint = joint;
            _parent = parent;
        }

        public override void Execute()
        {
            _joints.Add(_joint);
        }

        public override void Undo()
        {
            _joints.Remove(_joint);
        }

        public override string Description => $"Add Joint '{_joint.Name}'";
    }

    // ============================================================================
    // DELETE JOINT COMMAND
    // ============================================================================
    
    public class DeleteJointCommand : BaseUndoableCommand
    {
        private readonly ObservableCollection<JointModel> _joints;
        private readonly JointModel _joint;
        private readonly int _index;

        public DeleteJointCommand(ObservableCollection<JointModel> joints, JointModel joint)
        {
            _joints = joints;
            _joint = joint;
            _index = joints.IndexOf(joint);
        }

        public override void Execute()
        {
            _joints.Remove(_joint);
        }

        public override void Undo()
        {
            _joints.Insert(_index, _joint);
        }

        public override string Description => $"Delete Joint '{_joint.Name}'";
    }

    // ============================================================================
    // MOVE JOINT COMMAND (with merging for smooth dragging)
    // ============================================================================
    
    public class MoveJointCommand : BaseUndoableCommand
    {
        private readonly JointModel _joint;
        private SKPoint _oldPosition;
        private SKPoint _newPosition;

        public MoveJointCommand(JointModel joint, SKPoint oldPosition, SKPoint newPosition)
        {
            _joint = joint;
            _oldPosition = oldPosition;
            _newPosition = newPosition;
        }

        public override void Execute()
        {
            _joint.Position = _newPosition;
        }

        public override void Undo()
        {
            _joint.Position = _oldPosition;
        }

        public override string Description => $"Move Joint '{_joint.Name}'";

        // Allow merging consecutive move commands for same joint
        public override bool CanMergeWith(IUndoableCommand other)
        {
            if (other is MoveJointCommand moveCmd)
            {
                return moveCmd._joint == _joint;
            }
            return false;
        }

        public override void MergeWith(IUndoableCommand other)
        {
            if (other is MoveJointCommand moveCmd)
            {
                _newPosition = moveCmd._newPosition;
            }
        }
    }

    // ============================================================================
    // ADD VERTEX COMMAND
    // ============================================================================
    
    public class AddVertexCommand : BaseUndoableCommand
    {
        private readonly ObservableCollection<VertexModel> _vertices;
        private readonly VertexModel _vertex;

        public AddVertexCommand(ObservableCollection<VertexModel> vertices, VertexModel vertex)
        {
            _vertices = vertices;
            _vertex = vertex;
        }

        public override void Execute()
        {
            _vertices.Add(_vertex);
        }

        public override void Undo()
        {
            _vertices.Remove(_vertex);
        }

        public override string Description => $"Add Vertex #{_vertex.Id}";
    }

    // ============================================================================
    // DELETE VERTEX COMMAND
    // ============================================================================
    
    public class DeleteVertexCommand : BaseUndoableCommand
    {
        private readonly ObservableCollection<VertexModel> _vertices;
        private readonly VertexModel _vertex;
        private readonly int _index;

        public DeleteVertexCommand(ObservableCollection<VertexModel> vertices, VertexModel vertex)
        {
            _vertices = vertices;
            _vertex = vertex;
            _index = vertices.IndexOf(vertex);
        }

        public override void Execute()
        {
            _vertices.Remove(_vertex);
        }

        public override void Undo()
        {
            _vertices.Insert(_index, _vertex);
        }

        public override string Description => $"Delete Vertex #{_vertex.Id}";
    }

    // ============================================================================
    // MOVE VERTEX COMMAND
    // ============================================================================
    
    public class MoveVertexCommand : BaseUndoableCommand
    {
        private readonly VertexModel _vertex;
        private SKPoint _oldPosition;
        private SKPoint _newPosition;

        public MoveVertexCommand(VertexModel vertex, SKPoint oldPosition, SKPoint newPosition)
        {
            _vertex = vertex;
            _oldPosition = oldPosition;
            _newPosition = newPosition;
        }

        public override void Execute()
        {
            _vertex.BindPosition = _newPosition;
            _vertex.CurrentPosition = _newPosition;
        }

        public override void Undo()
        {
            _vertex.BindPosition = _oldPosition;
            _vertex.CurrentPosition = _oldPosition;
        }

        public override string Description => $"Move Vertex #{_vertex.Id}";

        public override bool CanMergeWith(IUndoableCommand other)
        {
            if (other is MoveVertexCommand moveCmd)
            {
                return moveCmd._vertex == _vertex;
            }
            return false;
        }

        public override void MergeWith(IUndoableCommand other)
        {
            if (other is MoveVertexCommand moveCmd)
            {
                _newPosition = moveCmd._newPosition;
            }
        }
    }

    // ============================================================================
    // ADD TRIANGLE COMMAND
    // ============================================================================
    
    public class AddTriangleCommand : BaseUndoableCommand
    {
        private readonly ObservableCollection<TriangleModel> _triangles;
        private readonly TriangleModel _triangle;

        public AddTriangleCommand(ObservableCollection<TriangleModel> triangles, TriangleModel triangle)
        {
            _triangles = triangles;
            _triangle = triangle;
        }

        public override void Execute()
        {
            _triangles.Add(_triangle);
        }

        public override void Undo()
        {
            _triangles.Remove(_triangle);
        }

        public override string Description => "Add Triangle";
    }

    // ============================================================================
    // DELETE TRIANGLE COMMAND
    // ============================================================================
    
    public class DeleteTriangleCommand : BaseUndoableCommand
    {
        private readonly ObservableCollection<TriangleModel> _triangles;
        private readonly TriangleModel _triangle;
        private readonly int _index;

        public DeleteTriangleCommand(ObservableCollection<TriangleModel> triangles, TriangleModel triangle)
        {
            _triangles = triangles;
            _triangle = triangle;
            _index = triangles.IndexOf(triangle);
        }

        public override void Execute()
        {
            _triangles.Remove(_triangle);
        }

        public override void Undo()
        {
            _triangles.Insert(_index, _triangle);
        }

        public override string Description => "Delete Triangle";
    }

    // ============================================================================
    // BATCH COMMAND (for multiple operations at once)
    // ============================================================================
    
    public class BatchCommand : BaseUndoableCommand
    {
        private readonly IUndoableCommand[] _commands;
        private readonly string _description;

        public BatchCommand(string description, params IUndoableCommand[] commands)
        {
            _description = description;
            _commands = commands;
        }

        public override void Execute()
        {
            foreach (var cmd in _commands)
                cmd.Execute();
        }

        public override void Undo()
        {
            // Undo in reverse order
            for (int i = _commands.Length - 1; i >= 0; i--)
                _commands[i].Undo();
        }

        public override string Description => _description;
    }
}





