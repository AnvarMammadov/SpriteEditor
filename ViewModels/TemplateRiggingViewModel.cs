using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using SkiaSharp;
using SpriteEditor.Data;
using SpriteEditor.Services.Rigging;
using SpriteEditor.Views;

namespace SpriteEditor.ViewModels
{
    /// <summary>
    /// Clean, template-only rigging ViewModel.
    /// Workflow: Load Sprite → Select Template → Fit Overlay → Bind → Physics Pose → Record Animation
    /// </summary>
    public partial class TemplateRiggingViewModel : ObservableObject
    {
        // === SERVICES ===
        private readonly TemplateService _templateService = new TemplateService();
        private readonly TemplateBindingService _bindingService = new TemplateBindingService();
        private readonly PhysicsService _physicsService = new PhysicsService();
        private readonly TemplateOverlayInteractionService _overlayInteractionService = new TemplateOverlayInteractionService();
        private readonly Services.Animation.AnimationRecorderService _animationRecorderService = new Services.Animation.AnimationRecorderService();
        
        // PERFORMANCE: Cache joint lookup dictionary to avoid LINQ in hot path
        private Dictionary<int, JointModel> _jointLookup = new Dictionary<int, JointModel>();
        
        // === ANIMATION ===
        public AnimationViewModel AnimationVM { get; private set; }

        // === SPRITE ===
        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(BindTemplateCommand))]
        [NotifyCanExecuteChangedFor(nameof(SelectTemplateCommand))]
        private SKBitmap _loadedSprite;

        [ObservableProperty]
        private bool _isSpriteLoaded;

        private string _loadedSpritePath;

        // === TEMPLATE ===
        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(BindTemplateCommand))]
        private RigTemplate _selectedTemplate;

        [ObservableProperty]
        private ObservableCollection<RigTemplate> _availableTemplates = new ObservableCollection<RigTemplate>();

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(BindTemplateCommand))]
        [NotifyCanExecuteChangedFor(nameof(SelectTemplateCommand))]
        [NotifyCanExecuteChangedFor(nameof(StartPhysicsCommand))]
        private bool _isTemplateBound;

        // === OVERLAY (Fitting Phase) ===
        [ObservableProperty]
        private bool _isOverlayVisible;

        [ObservableProperty]
        private SKPoint _overlayPosition = SKPoint.Empty;

        [ObservableProperty]
        private float _overlayScale = 1.0f;

        [ObservableProperty]
        private float _overlayRotation = 0f;

        [ObservableProperty]
        private float _overlayOpacity = 0.5f;

        [ObservableProperty]
        private bool _showOverlayHandles = true;

        private bool _isInteractingWithOverlay = false;
        
        // PERFORMANCE: Cache sprite bounds to avoid recalculating every frame
        private SKRectI _cachedSpriteBounds = SKRectI.Empty;
        private SKBitmap _lastBoundsSprite = null;

        // === SKELETON (After Binding) ===
        public ObservableCollection<JointModel> Joints { get; } = new ObservableCollection<JointModel>();

        // === OVERLAY JOINTS (During Fitting - for fine-tuning) ===
        public ObservableCollection<JointModel> OverlayJoints { get; } = new ObservableCollection<JointModel>();
        
        [ObservableProperty]
        private JointModel _selectedOverlayJoint;
        
        private bool _isDraggingOverlayJoint = false;
        private SKPoint _overlayJointDragOffset;

        // === MESH (From Template) ===
        public ObservableCollection<VertexModel> Vertices { get; } = new ObservableCollection<VertexModel>();
        public ObservableCollection<TriangleModel> Triangles { get; } = new ObservableCollection<TriangleModel>();

        // === PHYSICS ===
        [ObservableProperty]
        private bool _isPhysicsActive;

        [ObservableProperty]
        private float _gravity = 500f;

        [ObservableProperty]
        private float _damping = 0.98f;

        private DispatcherTimer _physicsTimer;
        private JointModel _draggedJoint;

        // === ANIMATION RECORDING ===
        [ObservableProperty]
        private bool _isRecording;

        [ObservableProperty]
        private int _recordedFrameCount;

        // === CAMERA ===
        [ObservableProperty]
        private SKPoint _cameraOffset = SKPoint.Empty;

        [ObservableProperty]
        private float _cameraScale = 1.0f;

        public event EventHandler RequestRedraw;

        public TemplateRiggingViewModel()
        {
            // Load available templates
            LoadAvailableTemplates();

            // Setup physics timer
            _physicsTimer = new DispatcherTimer();
            _physicsTimer.Interval = TimeSpan.FromMilliseconds(16); // 60 FPS
            _physicsTimer.Tick += PhysicsTimer_Tick;
            
            // Setup animation system
            AnimationVM = new AnimationViewModel(_animationRecorderService, () => Joints);
            AnimationVM.RequestRedraw += (s, e) => RequestRedraw?.Invoke(this, EventArgs.Empty);
        }

        // ========================================
        // === WORKFLOW COMMANDS ===
        // ========================================

        [RelayCommand]
        private void LoadSprite()
        {
            var dialog = new OpenFileDialog
            {
                Filter = "PNG Images (*.png)|*.png|All Files (*.*)|*.*",
                Title = "Load Sprite"
            };

            if (dialog.ShowDialog() == true)
            {
                try
                {
                    _loadedSpritePath = dialog.FileName;
                    byte[] fileBytes = File.ReadAllBytes(_loadedSpritePath);
                    using (var ms = new MemoryStream(fileBytes))
                    {
                        LoadedSprite = SKBitmap.Decode(ms);
                    }

                    if (LoadedSprite == null)
                        throw new Exception("Failed to decode image.");

                    IsSpriteLoaded = true;
                    ResetWorkflow();

                    CustomMessageBox.Show("Sprite loaded successfully!", "Success", MessageBoxButton.OK, MsgImage.Success);
                    RequestRedraw?.Invoke(this, EventArgs.Empty);
                }
                catch (Exception ex)
                {
                    CustomMessageBox.Show($"Failed to load sprite:\n{ex.Message}", "Error", MessageBoxButton.OK, MsgImage.Error);
                }
            }
        }

        [RelayCommand(CanExecute = nameof(CanSelectTemplate))]
        private void SelectTemplate(string templateName)
        {
            try
            {
                // Load template by name
                if (templateName == "Humanoid")
                {
                    SelectedTemplate = _templateService.LoadHumanoidTemplate();
                }
                else
                {
                    // Future: Load other templates
                    throw new NotImplementedException($"Template '{templateName}' not yet implemented");
                }

                IsOverlayVisible = true;

                // Center overlay on sprite
                if (LoadedSprite != null)
                {
                    OverlayPosition = new SKPoint(LoadedSprite.Width / 2f, LoadedSprite.Height / 2f);
                    OverlayScale = 1.0f;
                    OverlayRotation = 0f;
                    
                    // PERFORMANCE: Cache bounds on template load
                    _cachedSpriteBounds = DetectSpriteBounds(LoadedSprite);
                    _lastBoundsSprite = LoadedSprite;
                }

                // Create overlay joints for fine-tuning
                CreateOverlayJoints();

                CustomMessageBox.Show($"Template '{templateName}' selected.\n\n" +
                    $"1️⃣ Adjust overlay with handles\n" +
                    $"2️⃣ Drag individual joints to fine-tune\n" +
                    $"3️⃣ Click BIND when ready",
                    "Template Selected", MessageBoxButton.OK, MsgImage.Info);
                RequestRedraw?.Invoke(this, EventArgs.Empty);
            }
            catch (Exception ex)
            {
                CustomMessageBox.Show($"Failed to load template:\n{ex.Message}", "Error", MessageBoxButton.OK, MsgImage.Error);
            }
        }

        private bool CanSelectTemplate() => IsSpriteLoaded && !IsTemplateBound;

        [RelayCommand(CanExecute = nameof(CanBindTemplate))]
        private void BindTemplate()
        {
            try
            {
                // IMPORTANT: Use edited OverlayJoints if available, otherwise use template defaults
                Services.Rigging.BindingResult result;
                
                if (OverlayJoints.Count > 0)
                {
                    // User has edited joints - use their positions!
                    result = _bindingService.BindTemplateWithEditedJoints(
                        SelectedTemplate,
                        LoadedSprite,
                        OverlayJoints.ToList(),
                        OverlayPosition,
                        OverlayScale,
                        OverlayRotation
                    );
                }
                else
                {
                    // No edits - use template defaults with transform
                    result = _bindingService.BindTemplate(
                        SelectedTemplate,
                        LoadedSprite,
                        OverlayPosition,
                        OverlayScale,
                        OverlayRotation
                    );
                }

                // Apply binding result
                Joints.Clear();
                _jointLookup.Clear(); // PERFORMANCE: Clear cache
                
                foreach (var joint in result.Joints)
                {
                    Joints.Add(joint);
                    _jointLookup[joint.Id] = joint; // PERFORMANCE: Build O(1) lookup
                }

                Vertices.Clear();
                foreach (var vertex in result.Vertices)
                    Vertices.Add(vertex);

                Triangles.Clear();
                foreach (var triangle in result.Triangles)
                    Triangles.Add(triangle);

                // Update state
                IsTemplateBound = true;
                IsOverlayVisible = false;

                CustomMessageBox.Show(
                    $"Template bound successfully!\n\n" +
                    $"• {Joints.Count} joints\n" +
                    $"• {Vertices.Count} vertices\n" +
                    $"• {Triangles.Count} triangles\n\n" +
                    $"Ready for Physics Pose!",
                    "Success",
                    MessageBoxButton.OK,
                    MsgImage.Success
                );

                SelectTemplateCommand.NotifyCanExecuteChanged();
                BindTemplateCommand.NotifyCanExecuteChanged();
                StartPhysicsCommand.NotifyCanExecuteChanged();
                RequestRedraw?.Invoke(this, EventArgs.Empty);
            }
            catch (Exception ex)
            {
                CustomMessageBox.Show($"Binding failed:\n{ex.Message}", "Error", MessageBoxButton.OK, MsgImage.Error);
            }
        }

        private bool CanBindTemplate() => LoadedSprite != null && SelectedTemplate != null && !IsTemplateBound;

        [RelayCommand]
        private void Reset()
        {
            ResetWorkflow();
            RequestRedraw?.Invoke(this, EventArgs.Empty);
        }

        // ========================================
        // === PHYSICS COMMANDS ===
        // ========================================

        [RelayCommand(CanExecute = nameof(CanStartPhysics))]
        private void StartPhysics()
        {
            if (IsPhysicsActive) return;
            
            // Exit recording mode when entering physics mode
            if (AnimationVM.IsRecordingMode)
            {
                AnimationVM.IsRecordingMode = false;
            }

            _physicsService.Initialize(Joints);
            _physicsService.Gravity = Gravity;
            _physicsService.Damping = Damping;

            _physicsTimer.Start();
            IsPhysicsActive = true;
        }

        private bool CanStartPhysics() => IsTemplateBound && !IsPhysicsActive;

        [RelayCommand(CanExecute = nameof(CanStopPhysics))]
        private void StopPhysics()
        {
            if (!IsPhysicsActive) return;

            _physicsTimer.Stop();
            IsPhysicsActive = false;
            _draggedJoint = null;
            _physicsService.StopDragging();
        }

        private bool CanStopPhysics() => IsPhysicsActive;

        private void PhysicsTimer_Tick(object sender, EventArgs e)
        {
            _physicsService.VerletStep(deltaTime: 0.016f);
            
            // 1. Update Joint Rotations based on new positions
            UpdateJointRotations();

            // 2. CRITICAL: Update mesh vertices to follow skeleton (skinning)
            UpdateMeshVertices();
            
            RequestRedraw?.Invoke(this, EventArgs.Empty);
        }

        private void UpdateJointRotations()
        {
            foreach (var joint in Joints)
            {
                if (joint.Parent != null)
                {
                    float dx = joint.Position.X - joint.Parent.Position.X;
                    float dy = joint.Position.Y - joint.Parent.Position.Y;
                    joint.Rotation = MathF.Atan2(dy, dx);
                }
            }
        }

        /// <summary>
        /// CRITICAL: Update mesh vertices based on skeleton pose (Skinning/Deformation).
        /// Each vertex is influenced by multiple joints based on weights.
        /// </summary>
        private void UpdateMeshVertices()
        {
            if (Vertices.Count == 0 || _jointLookup.Count == 0) return;

            // PERFORMANCE: Avoid LINQ, use cached dictionary for O(1) joint lookup
            for (int i = 0; i < Vertices.Count; i++)
            {
                var vertex = Vertices[i];
                
                // Skip vertices with no joint influences
                if (vertex.Weights.Count == 0)
                {
                    vertex.CurrentPosition = vertex.BindPosition;
                    continue;
                }

                // Weighted blend position from all influencing joints
                float totalX = 0f;
                float totalY = 0f;
                float totalWeight = 0f;

                foreach (var weightEntry in vertex.Weights)
                {
                    int jointId = weightEntry.Key;
                    float influence = weightEntry.Value;

                    // PERFORMANCE: O(1) lookup instead of LINQ FirstOrDefault
                    if (_jointLookup.TryGetValue(jointId, out var joint))
                    {
                        // Linear Blend Skinning (LBS) with Rotation
                        // 1. Calculate local offset from joint in BIND pose
                        // Rotate bind offset by -BindRotation to get local axes aligned offset
                        float dx = vertex.BindPosition.X - joint.BindPosition.X;
                        float dy = vertex.BindPosition.Y - joint.BindPosition.Y;
                        
                        // Un-rotate by BindRotation
                        float cosBind = MathF.Cos(-joint.BindRotation);
                        float sinBind = MathF.Sin(-joint.BindRotation);
                        float localX = dx * cosBind - dy * sinBind;
                        float localY = dx * sinBind + dy * cosBind;

                        // 2. Apply CURRENT Rotation
                        float cosCurr = MathF.Cos(joint.Rotation);
                        float sinCurr = MathF.Sin(joint.Rotation);
                        float rotatedX = localX * cosCurr - localY * sinCurr;
                        float rotatedY = localX * sinCurr + localY * cosCurr;

                        // 3. Add to Current Joint Position
                        float finalX = joint.Position.X + rotatedX;
                        float finalY = joint.Position.Y + rotatedY;
                        
                        totalX += finalX * influence;
                        totalY += finalY * influence;
                        totalWeight += influence;
                    }
                }

                // Normalize if total weight != 1.0 (safety)
                if (totalWeight > 0.001f)
                {
                    vertex.CurrentPosition = new SKPoint(
                        totalX / totalWeight,
                        totalY / totalWeight
                    );
                }
                else
                {
                    vertex.CurrentPosition = vertex.BindPosition;
                }
            }
        }

        // ========================================
        // === HELPER METHODS (Restored) ===
        // ========================================

        private void LoadAvailableTemplates()
        {
            // For now, just Humanoid
            AvailableTemplates.Add(new RigTemplate { Name = "Humanoid" });
            // Future: Quadruped, etc.
        }

        private void ResetWorkflow()
        {
            StopPhysics();
            Joints.Clear();
            Vertices.Clear();
            Triangles.Clear();
            SelectedTemplate = null;
            IsTemplateBound = false;
            IsOverlayVisible = false;
            OverlayPosition = SKPoint.Empty;
            OverlayScale = 1.0f;
            OverlayRotation = 0f;

            SelectTemplateCommand?.NotifyCanExecuteChanged();
            BindTemplateCommand?.NotifyCanExecuteChanged();
            StartPhysicsCommand?.NotifyCanExecuteChanged();
            StopPhysicsCommand?.NotifyCanExecuteChanged();
        }

        // ========================================
        // === MOUSE INTERACTION ===
        // ========================================

        public void OnCanvasLeftClicked(SKPoint worldPos)
        {
            if (IsPhysicsActive)
            {
                // Find closest joint and start dragging (physics mode)
                var closestJoint = FindClosestJoint(worldPos);
                if (closestJoint != null)
                {
                    _draggedJoint = closestJoint;
                    _physicsService.StartDragging(closestJoint, worldPos);
                }
            }
            else if (IsOverlayVisible && SelectedTemplate != null)
            {
                // Priority 1: Check if clicking on overlay joint (fine-tuning)
                var clickedJoint = FindClosestOverlayJoint(worldPos, 15f);
                if (clickedJoint != null)
                {
                    SelectedOverlayJoint = clickedJoint;
                    _isDraggingOverlayJoint = true;
                    _overlayJointDragOffset = clickedJoint.Position - worldPos;
                    return;
                }
                
                // Priority 2: Overlay transform handles (drag/scale/rotate)
                var bounds = LoadedSprite != null 
                    ? new SKRect(0, 0, LoadedSprite.Width, LoadedSprite.Height)
                    : SKRect.Empty;
                
                _overlayInteractionService.BeginInteraction(
                    worldPos,
                    OverlayPosition,
                    OverlayScale,
                    OverlayRotation,
                    bounds
                );
                
                _isInteractingWithOverlay = (_overlayInteractionService.ActiveHandle != TemplateOverlayInteractionService.HandleType.None);
            }
        }

        public void OnCanvasMouseMoved(SKPoint worldPos)
        {
            if (IsPhysicsActive && _draggedJoint != null)
            {
                _physicsService.UpdateDragTarget(worldPos);
            }
            else if (_isDraggingOverlayJoint && SelectedOverlayJoint != null)
            {
                // Dragging individual overlay joint (fine-tuning)
                SelectedOverlayJoint.Position = new SKPoint(
                    worldPos.X + _overlayJointDragOffset.X,
                    worldPos.Y + _overlayJointDragOffset.Y
                );
                RequestRedraw?.Invoke(this, EventArgs.Empty);
            }
            else if (_isInteractingWithOverlay && IsOverlayVisible)
            {
                // Update overlay transform based on interaction
                var transform = _overlayInteractionService.UpdateInteraction(
                    worldPos,
                    OverlayPosition,
                    OverlayScale,
                    OverlayRotation
                );
                
                OverlayPosition = transform.Position;
                OverlayScale = transform.Scale;
                OverlayRotation = transform.Rotation;
                
                // PERFORMANCE: Only update joints if transform actually changed
                UpdateOverlayJointsTransform();
                
                RequestRedraw?.Invoke(this, EventArgs.Empty);
            }
        }

        public void OnCanvasLeftReleased()
        {
            if (_draggedJoint != null)
            {
                _physicsService.StopDragging();
                _draggedJoint = null;
            }
            else if (_isDraggingOverlayJoint)
            {
                _isDraggingOverlayJoint = false;
                SelectedOverlayJoint = null;
            }
            else if (_isInteractingWithOverlay)
            {
                _overlayInteractionService.EndInteraction();
                _isInteractingWithOverlay = false;
                
                // PERFORMANCE: Final update when interaction ends
                UpdateOverlayJointsTransform();
                RequestRedraw?.Invoke(this, EventArgs.Empty);
            }
        }

        private JointModel FindClosestJoint(SKPoint worldPos)
        {
            float minDist = float.MaxValue;
            JointModel closest = null;

            foreach (var joint in Joints)
            {
                float dx = joint.Position.X - worldPos.X;
                float dy = joint.Position.Y - worldPos.Y;
                float dist = dx * dx + dy * dy;

                if (dist < minDist)
                {
                    minDist = dist;
                    closest = joint;
                }
            }

            return (minDist < 400) ? closest : null; // 20px radius
        }

        // ========================================
        // === CAMERA (Simple) ===
        // ========================================

        public void ResetCamera()
        {
            CameraOffset = SKPoint.Empty;
            CameraScale = 1.0f;
        }

        public SKPoint ScreenToWorld(SKPoint screenPos)
        {
            return new SKPoint(
                (screenPos.X - CameraOffset.X) / CameraScale,
                (screenPos.Y - CameraOffset.Y) / CameraScale
            );
        }

        public SKPoint WorldToScreen(SKPoint worldPos)
        {
            return new SKPoint(
                (worldPos.X * CameraScale) + CameraOffset.X,
                (worldPos.Y * CameraScale) + CameraOffset.Y
            );
        }

        /// <summary>
        /// Get overlay interaction handles for rendering.
        /// </summary>
        public HandlePositions GetOverlayHandles()
        {
            if (!IsOverlayVisible || LoadedSprite == null)
                return null;

            var bounds = new SKRect(0, 0, LoadedSprite.Width, LoadedSprite.Height);
            return _overlayInteractionService.GetHandlePositions(OverlayPosition, OverlayScale, bounds);
        }

        /// <summary>
        /// Create overlay joints from template for fine-tuning.
        /// </summary>
        private void CreateOverlayJoints()
        {
            OverlayJoints.Clear();

            if (SelectedTemplate == null || LoadedSprite == null) return;

            var bounds = GetCachedSpriteBounds();
            float spriteWidth = bounds.Width;
            float spriteHeight = bounds.Height;
            float offsetX = bounds.Left;
            float offsetY = bounds.Top;

            var jointMap = new Dictionary<string, JointModel>();
            int idCounter = 0;

            // Create joints
            foreach (var templateJoint in SelectedTemplate.Joints)
            {
                float pixelX = offsetX + templateJoint.NormalizedPosition.X * spriteWidth;
                float pixelY = offsetY + templateJoint.NormalizedPosition.Y * spriteHeight;

                var joint = new JointModel(idCounter++, new SKPoint(pixelX, pixelY))
                {
                    Name = templateJoint.Name,
                    Mass = templateJoint.Mass,
                    IsAnchored = templateJoint.IsAnchored,
                    Stiffness = templateJoint.Stiffness
                };

                OverlayJoints.Add(joint);
                jointMap[templateJoint.Name] = joint;
            }

            // Set parent relationships
            for (int i = 0; i < SelectedTemplate.Joints.Count; i++)
            {
                var templateJoint = SelectedTemplate.Joints[i];
                var joint = OverlayJoints[i];

                if (!string.IsNullOrEmpty(templateJoint.ParentName) &&
                    jointMap.TryGetValue(templateJoint.ParentName, out var parent))
                {
                    joint.Parent = parent;
                }
            }
        }

        /// <summary>
        /// Update all overlay joints when global transform changes.
        /// </summary>
        private void UpdateOverlayJointsTransform()
        {
            if (SelectedTemplate == null || LoadedSprite == null) return;

            var bounds = GetCachedSpriteBounds();

            for (int i = 0; i < SelectedTemplate.Joints.Count && i < OverlayJoints.Count; i++)
            {
                var templateJoint = SelectedTemplate.Joints[i];
                var overlayJoint = OverlayJoints[i];

                // Transform template position with current overlay transform
                float spriteX = bounds.Left + templateJoint.NormalizedPosition.X * bounds.Width;
                float spriteY = bounds.Top + templateJoint.NormalizedPosition.Y * bounds.Height;

                // Apply scale
                float relX = (spriteX - OverlayPosition.X) * OverlayScale;
                float relY = (spriteY - OverlayPosition.Y) * OverlayScale;

                // Apply rotation
                if (MathF.Abs(OverlayRotation) > 0.001f)
                {
                    float cos = MathF.Cos(OverlayRotation);
                    float sin = MathF.Sin(OverlayRotation);
                    float rotX = relX * cos - relY * sin;
                    float rotY = relX * sin + relY * cos;
                    relX = rotX;
                    relY = rotY;
                }

                overlayJoint.Position = new SKPoint(
                    OverlayPosition.X + relX,
                    OverlayPosition.Y + relY
                );
            }
        }

        /// <summary>
        /// Find closest overlay joint to world position.
        /// </summary>
        private JointModel FindClosestOverlayJoint(SKPoint worldPos, float maxDistance)
        {
            float minDistSq = maxDistance * maxDistance;
            JointModel closest = null;

            foreach (var joint in OverlayJoints)
            {
                float dx = joint.Position.X - worldPos.X;
                float dy = joint.Position.Y - worldPos.Y;
                float distSq = dx * dx + dy * dy;

                if (distSq < minDistSq)
                {
                    minDistSq = distSq;
                    closest = joint;
                }
            }

            return closest;
        }

        /// <summary>
        /// Detect sprite bounds (non-transparent area).
        /// </summary>
        private SKRectI DetectSpriteBounds(SKBitmap sprite)
        {
            int minX = sprite.Width, maxX = 0;
            int minY = sprite.Height, maxY = 0;

            for (int y = 0; y < sprite.Height; y++)
            {
                for (int x = 0; x < sprite.Width; x++)
                {
                    var pixel = sprite.GetPixel(x, y);
                    if (pixel.Alpha > 10)
                    {
                        if (x < minX) minX = x;
                        if (x > maxX) maxX = x;
                        if (y < minY) minY = y;
                        if (y > maxY) maxY = y;
                    }
                }
            }


            if (minX > maxX || minY > maxY)
                return new SKRectI(0, 0, sprite.Width, sprite.Height);

            return new SKRectI(minX, minY, maxX, maxY);
        }
        
        /// <summary>
        /// PERFORMANCE: Get cached sprite bounds to avoid expensive recalculation.
        /// </summary>
        private SKRectI GetCachedSpriteBounds()
        {
            if (_lastBoundsSprite != LoadedSprite || _cachedSpriteBounds == SKRectI.Empty)
            {
                _cachedSpriteBounds = DetectSpriteBounds(LoadedSprite);
                _lastBoundsSprite = LoadedSprite;
            }
            return _cachedSpriteBounds;
        }

        // ========================================
        // === ANIMATION COMMANDS ===
        // ========================================

        [RelayCommand]
        private void ToggleRecordMode()
        {
            AnimationVM.IsRecordingMode = !AnimationVM.IsRecordingMode;
            
            if (AnimationVM.IsRecordingMode)
            {
                // Stop physics when entering record mode
                if (IsPhysicsActive)
                {
                    StopPhysics();
                }
                
                CustomMessageBox.Show(
                    "Recording Mode Enabled\n\n" +
                    "• Use Physics to pose your character\n" +
                    "• Click 'Record Keyframe' to save the pose\n" +
                    "• Create multiple keyframes to build your animation",
                    "Recording Mode",
                    System.Windows.MessageBoxButton.OK,
                    MsgImage.Info
                );
            }
        }

    }
}
