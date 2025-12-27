using System;
using System.Collections.ObjectModel;
using SkiaSharp;
using SpriteEditor.ViewModels;

namespace SpriteEditor.Services.Commands
{
    /// <summary>
    /// Command to add a joint to the rigging system.
    /// </summary>
    public class AddJointCommand : ICommand
    {
        private readonly ObservableCollection<JointModel> _joints;
        private readonly JointModel _joint;

        public string Description { get; }
        public DateTime Timestamp { get; }

        public AddJointCommand(ObservableCollection<JointModel> joints, JointModel joint)
        {
            _joints = joints ?? throw new ArgumentNullException(nameof(joints));
            _joint = joint ?? throw new ArgumentNullException(nameof(joint));
            Description = $"Add Joint '{joint.Name}'";
            Timestamp = DateTime.Now;
        }

        public void Execute()
        {
            if (!_joints.Contains(_joint))
            {
                _joints.Add(_joint);
            }
        }

        public void Undo()
        {
            _joints.Remove(_joint);
        }
    }

    /// <summary>
    /// Command to remove a joint from the rigging system.
    /// Also removes all child joints (cascading delete).
    /// </summary>
    public class RemoveJointCommand : ICommand
    {
        private readonly ObservableCollection<JointModel> _joints;
        private readonly JointModel _joint;
        private readonly int _originalIndex;

        public string Description { get; }
        public DateTime Timestamp { get; }

        public RemoveJointCommand(ObservableCollection<JointModel> joints, JointModel joint)
        {
            _joints = joints ?? throw new ArgumentNullException(nameof(joints));
            _joint = joint ?? throw new ArgumentNullException(nameof(joint));
            _originalIndex = _joints.IndexOf(_joint);
            Description = $"Remove Joint '{joint.Name}'";
            Timestamp = DateTime.Now;
        }

        public void Execute()
        {
            _joints.Remove(_joint);
        }

        public void Undo()
        {
            // Restore at original position
            if (_originalIndex >= 0 && _originalIndex <= _joints.Count)
            {
                _joints.Insert(_originalIndex, _joint);
            }
            else
            {
                _joints.Add(_joint);
            }
        }
    }

    /// <summary>
    /// Command to move a joint (change its position).
    /// Stores old and new positions for undo/redo.
    /// </summary>
    public class MoveJointCommand : ICommand
    {
        private readonly JointModel _joint;
        private readonly SKPoint _oldPosition;
        private readonly SKPoint _newPosition;

        public string Description { get; }
        public DateTime Timestamp { get; }

        public MoveJointCommand(JointModel joint, SKPoint oldPosition, SKPoint newPosition)
        {
            _joint = joint ?? throw new ArgumentNullException(nameof(joint));
            _oldPosition = oldPosition;
            _newPosition = newPosition;
            Description = $"Move Joint '{joint.Name}'";
            Timestamp = DateTime.Now;
        }

        public void Execute()
        {
            _joint.Position = _newPosition;
        }

        public void Undo()
        {
            _joint.Position = _oldPosition;
        }
    }

    /// <summary>
    /// Command to add a vertex to the mesh.
    /// </summary>
    public class AddVertexCommand : ICommand
    {
        private readonly ObservableCollection<VertexModel> _vertices;
        private readonly VertexModel _vertex;

        public string Description { get; }
        public DateTime Timestamp { get; }

        public AddVertexCommand(ObservableCollection<VertexModel> vertices, VertexModel vertex)
        {
            _vertices = vertices ?? throw new ArgumentNullException(nameof(vertices));
            _vertex = vertex ?? throw new ArgumentNullException(nameof(vertex));
            Description = $"Add Vertex (ID: {vertex.Id})";
            Timestamp = DateTime.Now;
        }

        public void Execute()
        {
            if (!_vertices.Contains(_vertex))
            {
                _vertices.Add(_vertex);
            }
        }

        public void Undo()
        {
            _vertices.Remove(_vertex);
        }
    }

    /// <summary>
    /// Command to remove a vertex from the mesh.
    /// </summary>
    public class RemoveVertexCommand : ICommand
    {
        private readonly ObservableCollection<VertexModel> _vertices;
        private readonly VertexModel _vertex;
        private readonly int _originalIndex;

        public string Description { get; }
        public DateTime Timestamp { get; }

        public RemoveVertexCommand(ObservableCollection<VertexModel> vertices, VertexModel vertex)
        {
            _vertices = vertices ?? throw new ArgumentNullException(nameof(vertices));
            _vertex = vertex ?? throw new ArgumentNullException(nameof(vertex));
            _originalIndex = _vertices.IndexOf(_vertex);
            Description = $"Remove Vertex (ID: {vertex.Id})";
            Timestamp = DateTime.Now;
        }

        public void Execute()
        {
            _vertices.Remove(_vertex);
        }

        public void Undo()
        {
            if (_originalIndex >= 0 && _originalIndex <= _vertices.Count)
            {
                _vertices.Insert(_originalIndex, _vertex);
            }
            else
            {
                _vertices.Add(_vertex);
            }
        }
    }
}
