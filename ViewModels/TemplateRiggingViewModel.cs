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
        private readonly TemplateOverlayInteractionService _overlayInteractionService = new TemplateOverlayInteractionService();
        private readonly KinematicService _kinematicService = new KinematicService();
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

        // === INTERACTION & IK ===
        [ObservableProperty]
        private bool _isIKMode = true; // IK mode enabled by default for intuitive posing
        
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

                // CRITICAL FIX: Calculate BindRotation for all joints IMMEDIATELY
                // This must happen BEFORE mesh skinning so vertices reference correct orientations
                CalculateBindRotations();

                // Update state
                IsTemplateBound = true;
                IsOverlayVisible = false;

                CustomMessageBox.Show(
                    $"Template bound successfully!\n\n" +
                    $"• {Joints.Count} joints\n" +
                    $"• {Vertices.Count} vertices\n" +
                    $"• {Triangles.Count} triangles\n\n" +
                    $"Ready for posing!",
                    "Success",
                    MessageBoxButton.OK,
                    MsgImage.Success
                );

                SelectTemplateCommand.NotifyCanExecuteChanged();
                BindTemplateCommand.NotifyCanExecuteChanged();
                RequestRedraw?.Invoke(this, EventArgs.Empty);
            }
            catch (Exception ex)
            {
                CustomMessageBox.Show($"Binding failed:\n{ex.Message}", "Error", MessageBoxButton.OK, MsgImage.Error);
            }
        }

        /// <summary>
        /// Calculate BindRotation for all joints based on their initial world positions.
        /// MUST be called after binding, BEFORE physics starts.
        /// </summary>
        private void CalculateBindRotations()
        {
            System.Diagnostics.Debug.WriteLine("=== BIND ROTATIONS ===");
            foreach (var joint in Joints)
            {
                if (joint.Parent != null)
                {
                    float dx = joint.Position.X - joint.Parent.Position.X;
                    float dy = joint.Position.Y - joint.Parent.Position.Y;
                    joint.BindRotation = MathF.Atan2(dy, dx);
                    joint.Rotation = joint.BindRotation; // Initialize current rotation too
                    
                    System.Diagnostics.Debug.WriteLine($"Joint {joint.Name}: BindPos=({joint.BindPosition.X:F1}, {joint.BindPosition.Y:F1}), BindRot={joint.BindRotation * 180f / MathF.PI:F1}°");
                }
                else
                {
                    joint.BindRotation = 0f;
                    joint.Rotation = 0f;
                    System.Diagnostics.Debug.WriteLine($"Joint {joint.Name} (ROOT): BindPos=({joint.BindPosition.X:F1}, {joint.BindPosition.Y:F1})");
                }
            }
            
            // DEBUG: Log first 3 vertices and their weights
            System.Diagnostics.Debug.WriteLine("=== VERTEX WEIGHTS (first 3) ===");
            for (int i = 0; i < Math.Min(3, Vertices.Count); i++)
            {
                var v = Vertices[i];
                System.Diagnostics.Debug.WriteLine($"Vertex {i}: BindPos=({v.BindPosition.X:F1}, {v.BindPosition.Y:F1}), Weights={v.Weights.Count}");
                foreach (var w in v.Weights)
                {
                    var j = Joints.FirstOrDefault(jnt => jnt.Id == w.Key);
                    System.Diagnostics.Debug.WriteLine($"  → {j?.Name ?? "?"}: {w.Value:F3}");
                }
            }
        }

        private bool CanBindTemplate() => LoadedSprite != null && SelectedTemplate != null && !IsTemplateBound;

        [RelayCommand]
        private void SaveCustomTemplate()
        {
            try
            {
                if (OverlayJoints.Count == 0 || SelectedTemplate == null)
                {
                    CustomMessageBox.Show("No template loaded to save", "Warning", MessageBoxButton.OK, MsgImage.Warning);
                    return;
                }

                var dialog = new Microsoft.Win32.SaveFileDialog
                {
                    Filter = "Template Files (*.template.json)|*.template.json",
                    Title = "Save Custom Template",
                    FileName = $"{SelectedTemplate.Name}.Custom.template.json"
                };

                if (dialog.ShowDialog() == true)
                {
                    _templateService.SaveCustomTemplate(SelectedTemplate, OverlayJoints.ToList(), dialog.FileName);
                    CustomMessageBox.Show("Template saved successfully!", "Success", MessageBoxButton.OK, MsgImage.Success);
                }
            }
            catch (Exception ex)
            {
                CustomMessageBox.Show($"Failed to save template:\n{ex.Message}", "Error", MessageBoxButton.OK, MsgImage.Error);
            }
        }

        [RelayCommand]
        private void LoadCustomTemplate()
        {
            try
            {
                var dialog = new Microsoft.Win32.OpenFileDialog
                {
                    Filter = "Template Files (*.template.json)|*.template.json|All Files (*.*)|*.*",
                    Title = "Load Custom Template"
                };

                if (dialog.ShowDialog() == true)
                {
                    SelectedTemplate = _templateService.LoadTemplate(dialog.FileName);
                    IsOverlayVisible = true;

                    if (LoadedSprite != null)
                    {
                        OverlayPosition = new SKPoint(LoadedSprite.Width / 2f, LoadedSprite.Height / 2f);
                        OverlayScale = 1.0f;
                        OverlayRotation = 0f;
                    }

                    CreateOverlayJoints();
                    CustomMessageBox.Show("Custom template loaded!", "Success", MessageBoxButton.OK, MsgImage.Success);
                    RequestRedraw?.Invoke(this, EventArgs.Empty);
                }
            }
            catch (Exception ex)
            {
                CustomMessageBox.Show($"Failed to load template:\n{ex.Message}", "Error", MessageBoxButton.OK, MsgImage.Error);
            }
        }

        [RelayCommand]
        private void Reset()
        {
            ResetWorkflow();
            RequestRedraw?.Invoke(this, EventArgs.Empty);
        }

        // ========================================
        // === POSING (Forward Kinematics) ===
        // ========================================
        // Note: IK (Inverse Kinematics) will be added in Phase 5

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
        }

        // ========================================
        // === MOUSE INTERACTION ===
        // ========================================

        public void OnCanvasLeftClicked(SKPoint worldPos)
        {
            // Priority 1: Bound skeleton joint dragging (for posing)
            if (IsTemplateBound && Joints.Count > 0)
            {
                var closestJoint = FindClosestJoint(worldPos);
                if (closestJoint != null)
                {
                    _draggedJoint = closestJoint;
                    return;
                }
            }
            
            // Priority 2: Overlay joint adjustment (before binding)
            if (IsOverlayVisible && SelectedTemplate != null)
            {
                // Check if clicking on overlay joint (fine-tuning)
                var clickedJoint = FindClosestOverlayJoint(worldPos, 15f);
                if (clickedJoint != null)
                {
                    SelectedOverlayJoint = clickedJoint;
                    _isDraggingOverlayJoint = true;
                    _overlayJointDragOffset = clickedJoint.Position - worldPos;
                    return;
                }
                
                // Overlay transform handles (drag/scale/rotate)
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
            if (_isDraggingOverlayJoint && SelectedOverlayJoint != null)
            {
                // Dragging individual overlay joint (fine-tuning before binding)
                SelectedOverlayJoint.Position = new SKPoint(
                    worldPos.X + _overlayJointDragOffset.X,
                    worldPos.Y + _overlayJointDragOffset.Y
                );
                RequestRedraw?.Invoke(this, EventArgs.Empty);
            }
            else if (_draggedJoint != null && IsTemplateBound)
            {
                // Dragging bound joint - use IK if mode enabled and joint has a chain
                if (IsIKMode && !string.IsNullOrEmpty(_draggedJoint.IKChainName))
                {
                    // Build IK chain from dragged joint to root
                    // OLD: var chain = _draggedJoint.GetChainToRoot();
                    // NEW: Only get the relevant limb chain
                    var chain = _draggedJoint.GetIKChain();
                    
                    System.Diagnostics.Debug.WriteLine($"IK DRAG: Joint={_draggedJoint.Name}, ChainName={_draggedJoint.IKChainName}, ChainLength={chain.Count}");
                    if (chain.Count > 0)
                    {
                        System.Diagnostics.Debug.WriteLine($"  Chain: {string.Join(" -> ", chain.Select(j => j.Name))}");
                    }
                    
                    if (chain.Count >= 2)
                    {
                        // Use CCD solver for all chain lengths
                        // Works well for 2-bone (simple), 3-bone (with wrist), and 4+ bone chains
                        System.Diagnostics.Debug.WriteLine($"  Solving IK to target: ({worldPos.X:F1}, {worldPos.Y:F1})");
                        _kinematicService.SolveCCD(chain, worldPos, maxIterations: 10, tolerance: 1f);
                        
                        // Update mesh vertices after IK
                        UpdateJointRotations();
                        UpdateMeshVertices();
                        System.Diagnostics.Debug.WriteLine($"  IK Solved. EndEffector now at: ({chain[0].Position.X:F1}, {chain[0].Position.Y:F1})");
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine($"  ERROR: Chain too short ({chain.Count} joints)");
                    }
                }
                else
                {
                    // Direct FK (Forward Kinematics) mode - just move the joint
                    System.Diagnostics.Debug.WriteLine($"FK DRAG: Joint={_draggedJoint.Name} to ({worldPos.X:F1}, {worldPos.Y:F1})");
                    _draggedJoint.Position = worldPos;
                    UpdateJointRotations();
                    UpdateMeshVertices();
                }
                
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
            // Release dragged joint
            if (_draggedJoint != null)
            {
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

            // Increased hit radius for easier clicking (40px radius = 1600px²)
            float hitRadius = 40f;
            bool withinRange = (minDist < (hitRadius * hitRadius));
            
            if (withinRange && closest != null)
            {
                System.Diagnostics.Debug.WriteLine($"Joint clicked: {closest.Name} at ({closest.Position.X:F1}, {closest.Position.Y:F1})");
            }
            
            return withinRange ? closest : null;
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
                    MinAngle = templateJoint.MinAngle,
                    MaxAngle = templateJoint.MaxAngle,
                    IKChainName = templateJoint.IKChainName
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
                // Info message about recording mode
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
