using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json; // JSON Serializasiyası üçün
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Animation;
using System.Windows.Threading; // Timer üçün lazımdır
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using SkiaSharp;
using SpriteEditor.Data; // Yaratdığımız Data modelləri üçün
using SpriteEditor.Views;
using TriangleNet.Geometry; // Triangle.NET üçün
using TriangleNet.Meshing;




namespace SpriteEditor.ViewModels
{
    public enum RiggingToolMode
    {
        Edit,
        CreateJoint,
        Pose,
        EditMesh,
    }

    public partial class RiggingViewModel : ObservableObject
    {

        // --- AutoWeight Parametrləri (UI-dan dəyişən) ---
        [ObservableProperty] private float _awSigmaFactor = 0.20f;   // 0.15–0.25 tipik
        [ObservableProperty] private float _awRadialPower = 1.0f;    // 0.8–1.2
        [ObservableProperty] private float _awLongPower = 0.5f;    // 0.3–0.8
        [ObservableProperty] private float _awMinKeep = 0.02f;   // 0.01–0.05
        [ObservableProperty] private int _awTopK = 4;       // 3–4
        [ObservableProperty] private float _awParentBlend = 0.25f;   // 0–0.5
        [ObservableProperty] private float _awAncestorDecay = 0.40f; // 0.2–0.6
        [ObservableProperty] private int _awSmoothIters = 3;       // 0–5
        [ObservableProperty] private float _awSmoothMu = 0.30f;   // 0.1–0.5


        // Default-a qaytarmaq üçün
        [RelayCommand]
        private void ResetAutoWeightDefaults()
        {
            AwSigmaFactor = 0.20f;
            AwRadialPower = 1.0f;
            AwLongPower = 0.5f;
            AwMinKeep = 0.02f;
            AwTopK = 4;
            AwParentBlend = 0.5f;
            AwAncestorDecay = 0.40f;
            AwSmoothIters = 3;
            AwSmoothMu = 0.55f;
        }

        public event EventHandler RequestRedraw;
        public event EventHandler RequestCenterCamera;

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(LoadImageCommand))]
        [NotifyCanExecuteChangedFor(nameof(SaveRigCommand))]
        [NotifyCanExecuteChangedFor(nameof(LoadRigCommand))]
       // [NotifyCanExecuteChangedFor(nameof(AutoGenerateVerticesCommand))]
        private bool _isImageLoaded = false;

        [ObservableProperty]
        private SKBitmap _loadedBitmap;

        [ObservableProperty]
        private RiggingToolMode _currentTool = RiggingToolMode.Edit;

        // === SKELET MƏLUMATLARI ===
        public ObservableCollection<JointModel> Joints { get; } = new ObservableCollection<JointModel>();

        [ObservableProperty]
        private JointModel _selectedJoint;

        // === MESH MƏLUMATLARI ===
        public ObservableCollection<VertexModel> Vertices { get; } = new ObservableCollection<VertexModel>();
        public ObservableCollection<TriangleModel> Triangles { get; } = new ObservableCollection<TriangleModel>();

        [ObservableProperty]
        private VertexModel _selectedVertex;

        public ObservableCollection<VertexModel> VertexSelectionForTriangle { get; } = new ObservableCollection<VertexModel>();

        [ObservableProperty]
        private SKPoint _currentMousePosition;

        private int _jointIdCounter = 0;
        private int _vertexIdCounter = 0;
        private string _loadedImagePath;

        // === KAMERA VƏZİYYƏTİ ===
        public SKPoint CameraOffset { get; private set; } = SKPoint.Empty;
        public float CameraScale { get; private set; } = 1.0f;

        private bool _isPanning = false;
        private SKPoint _lastPanPosition;

        // === SÜRÜKLƏMƏ VƏZİYYƏTLƏRİ ===
        private bool _isDraggingJoint = false;
        private SKPoint _dragOffset;
        private bool _isDraggingVertex = false;
        private SKPoint _vertexDragOffset;

        // === YENİ (PLAN 3 - FINAL): "SAKİT VƏZİYYƏT" (BIND POSE) YADDAŞI ===
        private Dictionary<int, SKPoint> _jointBindPositions = new Dictionary<int, SKPoint>();
        private Dictionary<int, float> _jointBindRotations = new Dictionary<int, float>();
        // ==============================================================

        private const float EPS = 1e-4f;

        // === ANİMASİYA SİSTEMİ ===

        [ObservableProperty]
        private ObservableCollection<AnimationClipData> _animations = new ObservableCollection<AnimationClipData>();

        [ObservableProperty]
        private AnimationClipData _currentAnimation; // Hazırda seçili animasiya

        [ObservableProperty]
        private double _currentTime = 0.0; // Zaman çubuğu (Slider dəyəri)

        [ObservableProperty]
        private double _totalDuration = 2.0; // Animasiya uzunluğu

        [ObservableProperty]
        private bool _isPlaying = false;

        // Bu dəyişən slider-i əllə çəkəndə lazımdır ki, hər dəfə render etsin
        partial void OnCurrentTimeChanged(double value)
        {
            ApplyAnimationAtTime((float)value);
        }

        // === Timer Dəyişəni ===
        private DispatcherTimer _animationTimer;

        private void SetupTimer()
        {
            _animationTimer = new DispatcherTimer();
            _animationTimer.Interval = TimeSpan.FromMilliseconds(16); // ~60 FPS
            _animationTimer.Tick += AnimationTimer_Tick;
        }
        public RiggingViewModel()
        {

            SetupTimer();
             // (İstəyə bağlı) başlanğıc AutoWeight presetləri
             // Bunları istəmirsənsə, silə bilərsən.
             AwSigmaFactor = 0.20f;
            AwRadialPower = 1.0f;
            AwLongPower = 0.5f;
            AwMinKeep = 0.02f;
            AwTopK = 4;
            AwParentBlend = 0.25f;
            AwAncestorDecay = 0.40f;
            AwSmoothIters = 3;
            AwSmoothMu = 0.30f;
            // Kolleksiya dəyişikliklərini izləyək ki, UI-dakı düymələr vaxtında aktivləşsin/deaktivləşsin
            Joints.CollectionChanged += (_, __) =>
            {
               // AutoGenerateVerticesCommand?.NotifyCanExecuteChanged();
                AutoWeightCommand?.NotifyCanExecuteChanged();
                AutoTriangleCommand?.NotifyCanExecuteChanged();
                SaveRigCommand?.NotifyCanExecuteChanged();
            };

            Vertices.CollectionChanged += (_, __) =>
            {
                AutoWeightCommand?.NotifyCanExecuteChanged();
                AutoTriangleCommand?.NotifyCanExecuteChanged();
                SaveRigCommand?.NotifyCanExecuteChanged();
            };

            Triangles.CollectionChanged += (_, __) =>
            {
                SaveRigCommand?.NotifyCanExecuteChanged();
            };

            // İlk vəziyyət üçün (əgər VM yaradılan kimi UI binding artıq qurulubsa)
           // AutoGenerateVerticesCommand?.NotifyCanExecuteChanged();
            AutoWeightCommand?.NotifyCanExecuteChanged();
            AutoTriangleCommand?.NotifyCanExecuteChanged();
            SaveRigCommand?.NotifyCanExecuteChanged();
        }





        // === YENİ (PLAN 3): AVTO AĞIRLIQLANDIRMA ƏMRİ ===
        [RelayCommand(CanExecute = nameof(CanAutoWeight))]
        private void AutoWeight()
        {



            if (!CanAutoWeight()) return;

            // Parametrlər (istəyə görə tənzimlə)
            float sigmaFactor = AwSigmaFactor;          // σ = boneLength * sigmaFactor (0.15–0.25 yaxşıdır)
            float radialPower = AwRadialPower;        // f_r ^ radialPower
            float longPower = AwLongPower;      // f_l ^ longPower
            float minKeep = AwMinKeep;        // çox kiçik çəkiləri at
            int topK = AwTopK;           // hər vertex üçün ən çox N sümük
            float parentBlend = AwParentBlend;    // valideynə pay (birinci ata)
            float ancestorDecay = AwAncestorDecay;  // daha yuxarı ancestorlara eksponensial azalma
            int smoothIters = AwSmoothIters;    // smoothing iterasiyası
            float smoothMu = AwSmoothMu;       // smoothing qarışdırma əmsalı

            // 1) Sümükləri (seqmentləri) hazırla (Bind pose-da işlədiyindən əmin ol)
            var bones = BuildBoneSegments(Joints);
            if (bones.Count == 0) return;

            // 2) Hər vertex üçün raw çəkilər (bone->weight)
            foreach (var v in Vertices)
            {
                var raw = new Dictionary<int, float>(); // jointId -> weight (uşaq oynaq ID-si)
                foreach (var b in bones)
                {
                    // Sümüyə (P->C) məsafəyə görə radial təsir + uzunluq boyu window
                    float t, dist;
                    ProjectToSegment(v.BindPosition, b.P, b.C, out t, out dist);

                    // Radial gaussian təsir
                    float len = Distance(b.P, b.C);
                    float sigma = MathF.Max(1e-3f, len * sigmaFactor);
                    float fr = 1.0f / (1.0f + (dist / sigma) * (dist / sigma)); // daha linear, daha yumşa
                    fr = MathF.Pow(fr, radialPower);

                    // Uzunluq boyu "window" – mərkəzdə bir az güclü (ucda yumşalır)
                    // 0..1 aralığında cosine window: max t=0.5, min t≈0/1
                    float fl = 0.5f * (1f + MathF.Cos(MathF.PI * MathF.Abs(2f * t - 1f)));
                    fl = MathF.Pow(fl, longPower);

                    float w = fr * fl;
                    if (w <= 0f) continue;

                    // Çəkini uşaq oynağın ID-sinə yazırıq (bone.ChildId)
                    AddWeight(raw, b.ChildId, w);
                }

                // 3) Zəncir paylanması (parent/ancestor-lara azca pay)
                if (raw.Count > 0)
                {
                    var blended = new Dictionary<int, float>(raw);
                    foreach (var kv in raw)
                    {
                        int jointId = kv.Key;
                        float w = kv.Value;
                        float carry = w * parentBlend;
                        var cur = FindJointById(jointId);
                        float factor = 1.0f;
                        while (carry > 1e-5f && cur != null && cur.Parent != null)
                        {
                            cur = cur.Parent;
                            factor *= ancestorDecay;
                            float give = carry * factor;
                            if (give <= 1e-5f) break;
                            AddWeight(blended, cur.Id, give);
                        }
                    }
                    v.Weights = blended;
                }
                else
                {
                    v.Weights.Clear();
                }

                // 4) Top-K, threshold və normalizasiya
                PruneAndNormalize(v.Weights, topK, minKeep);
            }

            // 5) Mesh qonşuluğunda weight smoothing (Laplacian-vari)
            if (Triangles.Count > 0 && Vertices.Count > 0 && smoothIters > 0)
            {
                var neighbors = BuildVertexNeighbors(Vertices, Triangles);
                for (int it = 0; it < smoothIters; it++)
                {
                    SmoothWeightsOnce(Vertices, neighbors, smoothMu, topK, minKeep);
                }
            }
            // === YENİ ƏLAVƏ: XƏBƏRDARLIQ ===
            else if (smoothIters > 0 && Triangles.Count == 0)
            {
                // Yumşaltma istənilib, amma üçbucaq yoxdur
                CustomMessageBox.Show("Str_Msg_WarnSmoothing", "Str_Title_Warning", MessageBoxButton.OK, MsgImage.Warning);
            }
            // ================================

            SaveRigCommand.NotifyCanExecuteChanged();
            CustomMessageBox.Show("Str_Msg_SuccessAutoWeight", "Str_Title_Success", MessageBoxButton.OK, MsgImage.Success);
        }

        private bool CanAutoWeight()
        {
            return IsImageLoaded && Joints.Count > 0 && Vertices.Count > 0;
        }

        // ======================= Köməkçilər =======================

        private sealed class BoneSeg
        {
            public int ChildId;
            public SKPoint P; // parent position (bind)
            public SKPoint C; // child position  (bind)
        }

        private static float Distance(in SKPoint a, in SKPoint b)
        {
            float dx = a.X - b.X; float dy = a.Y - b.Y;
            return MathF.Sqrt(dx * dx + dy * dy);
        }

        private List<BoneSeg> BuildBoneSegments(IEnumerable<JointModel> joints)
        {
            // Bind pose-da olduğundan əmin ol: RecomputeBoneParamsFromPositions() çağırılıb
            var list = new List<BoneSeg>();
            foreach (var j in joints)
            {
                if (j.Parent == null) continue;
                list.Add(new BoneSeg
                {
                    ChildId = j.Id,
                    P = j.Parent.Position,
                    C = j.Position
                });
            }
            return list;
        }

        private JointModel FindJointById(int id)
        {
            // Joints çox böyük deyil; sadə axtarış kifayət edir
            foreach (var j in Joints) if (j.Id == id) return j;
            return null;
        }

        private static void ProjectToSegment(in SKPoint V, in SKPoint P, in SKPoint C, out float t, out float dist)
        {
            float vx = V.X - P.X, vy = V.Y - P.Y;
            float cx = C.X - P.X, cy = C.Y - P.Y;
            float denom = (cx * cx + cy * cy);
            if (denom < 1e-8f)
            {
                t = 0f;
                dist = MathF.Sqrt(vx * vx + vy * vy);
                return;
            }
            float dot = vx * cx + vy * cy;
            t = dot / denom;
            if (t < 0f) t = 0f; else if (t > 1f) t = 1f;
            float qx = P.X + t * cx, qy = P.Y + t * cy;
            float dx = V.X - qx, dy = V.Y - qy;
            dist = MathF.Sqrt(dx * dx + dy * dy);
        }

        private static void AddWeight(Dictionary<int, float> dict, int jointId, float w)
        {
            if (dict.TryGetValue(jointId, out var cur)) dict[jointId] = cur + w;
            else dict[jointId] = w;
        }

        private static void PruneAndNormalize(Dictionary<int, float> weights, int topK, float minKeep)
        {
            if (weights.Count == 0) return;

            // Sırala və Top-K saxla
            var sorted = weights.OrderByDescending(kv => kv.Value).Take(topK)
                                .Where(kv => kv.Value >= minKeep).ToList();

            weights.Clear();
            float sum = 0f;
            foreach (var kv in sorted) { weights[kv.Key] = kv.Value; sum += kv.Value; }
            if (sum < 1e-8f) { weights.Clear(); return; }
            // Normalizasiya
            var keys = weights.Keys.ToList();
            foreach (var k in keys) weights[k] = weights[k] / sum;
        }

        private static Dictionary<VertexModel, HashSet<VertexModel>> BuildVertexNeighbors(
            IEnumerable<VertexModel> verts,
            IEnumerable<TriangleModel> tris)
        {
            var adj = new Dictionary<VertexModel, HashSet<VertexModel>>();
            void link(VertexModel a, VertexModel b)
            {
                if (!adj.TryGetValue(a, out var set)) { set = new HashSet<VertexModel>(); adj[a] = set; }
                set.Add(b);
            }

            foreach (var t in tris)
            {
                link(t.V1, t.V2); link(t.V2, t.V1);
                link(t.V2, t.V3); link(t.V3, t.V2);
                link(t.V3, t.V1); link(t.V1, t.V3);
            }

            // indisi olmayan vertexlər də boş set alsın
            foreach (var v in verts) if (!adj.ContainsKey(v)) adj[v] = new HashSet<VertexModel>();
            return adj;
        }

        private static void SmoothWeightsOnce(
            IEnumerable<VertexModel> verts,
            Dictionary<VertexModel, HashSet<VertexModel>> neighbors,
            float mu, int topK, float minKeep)
        {
            // Yeni çəkilər üçün keçid buffer
            var newWeights = new Dictionary<VertexModel, Dictionary<int, float>>();

            foreach (var v in verts)
            {
                // Qonşu düyünlərin çəkilərini ortalaşdır
                if (!neighbors.TryGetValue(v, out var nb) || nb.Count == 0)
                {
                    // Qonşu yoxdursa, eyni saxla
                    newWeights[v] = new Dictionary<int, float>(v.Weights);
                    continue;
                }

                // Qonşuların birlikdə istifadə etdiyi sümüklərin (jointId) birləşmiş dəsti
                var unionJoints = new HashSet<int>(v.Weights.Keys);
                foreach (var u in nb) foreach (var jid in u.Weights.Keys) unionJoints.Add(jid);

                var averaged = new Dictionary<int, float>();
                foreach (var jid in unionJoints)
                {
                    float sum = 0f; int cnt = 0;
                    foreach (var u in nb)
                    {
                        if (u.Weights.TryGetValue(jid, out var wu)) { sum += wu; cnt++; }
                    }
                    float neighborAvg = (cnt > 0) ? (sum / cnt) : 0f;
                    float self = v.Weights.TryGetValue(jid, out var ws) ? ws : 0f;
                    float blended = (1f - mu) * self + mu * neighborAvg;
                    if (blended > 0f) averaged[jid] = blended;
                }

                PruneAndNormalize(averaged, topK, minKeep);
                newWeights[v] = averaged;
            }

            // Geri yaz
            foreach (var v in verts)
            {
                v.Weights = newWeights[v];
            }
        }

        // =======================================================


        [RelayCommand]
        private void LoadImage()
        {
            OpenFileDialog openDialog = new OpenFileDialog
            {
                Filter = "Image Files (*.png)|*.png|All Files (*.*)|*.*"
            };

            if (openDialog.ShowDialog() == true)
            {
                try
                {
                    _loadedImagePath = openDialog.FileName;
                    byte[] fileBytes = File.ReadAllBytes(_loadedImagePath);
                    using (var ms = new MemoryStream(fileBytes))
                    {
                        LoadedBitmap = SKBitmap.Decode(ms);
                    }
                    if (LoadedBitmap == null)
                    {
                        throw new Exception(App.GetStr("Str_Err_FileCorrupt"));
                    }
                    IsImageLoaded = true;
                    ClearRiggingData();
                    ResetCamera();
                    RequestCenterCamera?.Invoke(this, EventArgs.Empty);
                }
                catch (Exception ex)
                {
                    CustomMessageBox.Show(
    App.GetStr("Str_Msg_ErrRigLoad", ex.Message),
    "Str_Title_Error",
    MessageBoxButton.OK,
    MsgImage.Error);
                    IsImageLoaded = false;
                    LoadedBitmap = null;
                    _loadedImagePath = null;
                    ClearRiggingData();
                    ResetCamera();
                }
            }
        }


        // === Skeleti Yüklə (PLAN 3 - MESH ƏLAVƏ EDİLDİ) ===
        [RelayCommand(CanExecute = nameof(CanLoadRig))]
        private async Task LoadRigAsync()
        {
            if (!CanLoadRig()) return;

            string rigFilter = App.GetStr("Str_Filter_RigJson");
            string allFiles = App.GetStr("Str_Filter_AllFiles");
            OpenFileDialog openDialog = new OpenFileDialog
            {
                Filter = $"{rigFilter} (*.rig.json)|*.rig.json|{allFiles} (*.*)|*.*",
                Title = App.GetStr("Str_Dlg_LoadRig") // Və ya SaveRig üçün Str_Dlg_SaveRig

            };

            if (openDialog.ShowDialog() == true)
            {
                try
                {
                    string jsonString = await File.ReadAllTextAsync(openDialog.FileName);
                    var rigData = JsonSerializer.Deserialize<RigData>(jsonString);

                    if (rigData == null)
                    {
                        throw new Exception("JSON faylının strukturu düzgün deyil.");
                    }

                    string jsonImageName = rigData.ImageFileName;
                    string currentImageName = Path.GetFileName(_loadedImagePath);
                    if (jsonImageName != currentImageName)
                    {
                        var result = MessageBox.Show(
                            $"Bu skelet faylı ('{jsonImageName}') yüklənmiş şəkildən ('{currentImageName}') fərqli bir şəkil üçün yaradılıb.\n\nDavam etmək istəyirsinizmi?",
                            "Xəbərdarlıq", MessageBoxButton.YesNo, MessageBoxImage.Warning);

                        if (result == MessageBoxResult.No)
                            return;
                    }

                    ClearRiggingData();

                    var jointMap = new Dictionary<int, JointModel>();
                    if (rigData.Joints != null)
                    {
                        foreach (var jointData in rigData.Joints)
                        {
                            var newJoint = new JointModel(jointData.Id, jointData.Position, null)
                            {
                                BoneLength = jointData.BoneLength,
                                Rotation = jointData.Rotation
                            };

                            if (!string.IsNullOrEmpty(jointData.Name))
                            {
                                newJoint.Name = jointData.Name;
                            }
                            Joints.Add(newJoint);
                            jointMap.Add(newJoint.Id, newJoint);
                        }
                        foreach (var jointData in rigData.Joints)
                        {
                            if (jointData.ParentId != -1)
                            {
                                if (jointMap.TryGetValue(jointData.Id, out JointModel currentJoint) &&
                                    jointMap.TryGetValue(jointData.ParentId, out JointModel parentJoint))
                                {
                                    currentJoint.Parent = parentJoint;
                                }
                            }
                        }
                        if (Joints.Count > 0)
                        {
                            _jointIdCounter = Joints.Max(j => j.Id) + 1;
                        }
                        RecomputeBoneParamsFromPositions();
                    }

                    var vertexMap = new Dictionary<int, VertexModel>();
                    if (rigData.Mesh != null)
                    {
                        foreach (var vertexData in rigData.Mesh.Vertices)
                        {
                            var newVertex = new VertexModel(vertexData.Id, vertexData.Position)
                            {
                                Weights = vertexData.Weights
                            };
                            Vertices.Add(newVertex);
                            vertexMap.Add(newVertex.Id, newVertex);
                        }
                        foreach (var triangleData in rigData.Mesh.Triangles)
                        {
                            if (vertexMap.TryGetValue(triangleData.V1, out var v1) &&
                                vertexMap.TryGetValue(triangleData.V2, out var v2) &&
                                vertexMap.TryGetValue(triangleData.V3, out var v3))
                            {
                                Triangles.Add(new TriangleModel(v1, v2, v3));
                            }
                        }
                        if (Vertices.Count > 0)
                        {
                            _vertexIdCounter = Vertices.Max(v => v.Id) + 1;
                        }
                    }

                    RequestRedraw?.Invoke(this, EventArgs.Empty);
                    SaveRigCommand.NotifyCanExecuteChanged();
                    AutoWeightCommand.NotifyCanExecuteChanged();
                    AutoTriangleCommand.NotifyCanExecuteChanged();
                    // AutoGenerateVerticesCommand.NotifyCanExecuteChanged();

                    CustomMessageBox.Show("Str_Msg_SuccessRigLoad", "Str_Title_Success", MessageBoxButton.OK, MsgImage.Success);
                }
                catch (Exception ex)
                {
                    CustomMessageBox.Show(
     App.GetStr("Str_Msg_ErrRigLoad", ex.Message),
     "Str_Title_Error",
     MessageBoxButton.OK,
     MsgImage.Error);
                    ClearRiggingData();
                    RequestRedraw?.Invoke(this, EventArgs.Empty);
                }
            }
        }



        private bool CanLoadRig()
        {
            return IsImageLoaded;
        }


        [RelayCommand(CanExecute = nameof(CanSaveRig))]
        private async Task SaveRigAsync()
        {
            if (!CanSaveRig()) return;
            var rigData = new RigData { ImageFileName = Path.GetFileName(_loadedImagePath) };
            foreach (var joint in Joints)
            {
                rigData.Joints.Add(new JointData
                {
                    Id = joint.Id,
                    Position = joint.Position, // Həmişə "sakit" vəziyyəti saxla
                    ParentId = joint.Parent?.Id ?? -1,
                    BoneLength = joint.BoneLength,
                    Rotation = joint.Rotation, // Bu, "sakit" vəziyyətdəki bucaqdır
                    Name = joint.Name
                });
            }
            foreach (var vertex in Vertices)
            {
                rigData.Mesh.Vertices.Add(new VertexData
                {
                    Id = vertex.Id,
                    Position = vertex.BindPosition,
                    Weights = vertex.Weights
                });
            }
            foreach (var triangle in Triangles)
            {
                rigData.Mesh.Triangles.Add(new TriangleData
                {
                    V1 = triangle.V1.Id,
                    V2 = triangle.V2.Id,
                    V3 = triangle.V3.Id
                });
            }

            SaveFileDialog saveDialog = new SaveFileDialog
            {
                FileName = $"{Path.GetFileNameWithoutExtension(_loadedImagePath)}.rig.json",
                Filter = "Rig JSON Faylı (*.rig.json)|*.rig.json|All Files (*.*)|*.*",
                Title = "Save Bone Data"
            };

            if (saveDialog.ShowDialog() == true)
            {
                try
                {
                    var options = new JsonSerializerOptions { WriteIndented = true };
                    string jsonString = JsonSerializer.Serialize(rigData, options);
                    await File.WriteAllTextAsync(saveDialog.FileName, jsonString);
                    CustomMessageBox.Show("Str_Msg_SuccessRigSave", "Str_Title_Success", MessageBoxButton.OK, MsgImage.Success);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Skeleti yadda saxlayarkən xəta baş verdi: {ex.Message}", "Xəta");
                }
            }
        }


        private bool CanSaveRig()
        {
            return IsImageLoaded && (Joints.Count > 0 || Vertices.Count > 0);
        }

        #region Camera Controls
        public void ResetCamera()
        {
            CameraOffset = SKPoint.Empty;
            CameraScale = 1.0f;
        }
        public void CenterCamera(float canvasWidth, float canvasHeight, bool forceRecenter = false)
        {
            if (LoadedBitmap == null) return;
            if (forceRecenter || (CameraScale == 1.0f && CameraOffset == SKPoint.Empty))
            {
                float offsetX = (canvasWidth - (LoadedBitmap.Width * CameraScale)) / 2;
                float offsetY = (canvasHeight - (LoadedBitmap.Height * CameraScale)) / 2;
                CameraOffset = new SKPoint(offsetX, offsetY);
                RequestRedraw?.Invoke(this, EventArgs.Empty);
            }
        }
        public SKPoint ScreenToWorld(SKPoint screenPoint)
        {
            return new SKPoint(
                (screenPoint.X - CameraOffset.X) / CameraScale,
                (screenPoint.Y - CameraOffset.Y) / CameraScale
            );
        }
        public SKPoint WorldToScreen(SKPoint worldPoint)
        {
            return new SKPoint(
                (worldPoint.X * CameraScale) + CameraOffset.X,
                (worldPoint.Y * CameraScale) + CameraOffset.Y
            );
        }
        public void StartPan(SKPoint screenPos)
        {
            _isPanning = true;
            _lastPanPosition = screenPos;
        }
        public void StopPan()
        {
            _isPanning = false;
        }
        public void HandleZoom(SKPoint screenPos, int delta)
        {
            float zoomFactor = 1.1f;
            float newScale;
            if (delta > 0) newScale = CameraScale * zoomFactor;
            else newScale = CameraScale / zoomFactor;
            newScale = Math.Max(0.1f, Math.Min(newScale, 10.0f));
            if (Math.Abs(newScale - CameraScale) < 0.001f) return;
            SKPoint worldPosBefore = ScreenToWorld(screenPos);
            CameraScale = newScale;
            CameraOffset = new SKPoint(
                screenPos.X - (worldPosBefore.X * CameraScale),
                screenPos.Y - (worldPosBefore.Y * CameraScale)
            );
            RequestRedraw?.Invoke(this, EventArgs.Empty);
        }
        #endregion

        // === OnCanvasLeftClicked (YENİLƏNMİŞ - Ctrl+Klik Məntiqi) ===
        public void OnCanvasLeftClicked(SKPoint screenPos, bool isCtrlPressed)
        {
            SKPoint worldPos = ScreenToWorld(screenPos);
            float clickRadiusScreen = 10f;
            float clickRadiusWorld = clickRadiusScreen / CameraScale;
            float clickRadiusSq = clickRadiusWorld * clickRadiusWorld;

            if (CurrentTool == RiggingToolMode.CreateJoint)
            {
                var newJoint = new JointModel(_jointIdCounter++, worldPos, SelectedJoint);
                if (newJoint.Parent != null)
                {
                    float dx = newJoint.Position.X - newJoint.Parent.Position.X;
                    float dy = newJoint.Position.Y - newJoint.Parent.Position.Y;
                    float len = MathF.Sqrt(dx * dx + dy * dy);
                    newJoint.BoneLength = (len < EPS) ? 0f : len;
                    newJoint.Rotation = (len < EPS) ? 0f : MathF.Atan2(dy, dx);
                }
                AddJoint(newJoint); // DƏYİŞİKLİK
                SelectedJoint = newJoint;
                SaveRigCommand.NotifyCanExecuteChanged();
            }
            else if (CurrentTool == RiggingToolMode.Edit || CurrentTool == RiggingToolMode.Pose)
            {
                JointModel closestJoint = FindClosestJoint(worldPos, clickRadiusSq);
                SelectedJoint = closestJoint;
                if (SelectedJoint != null)
                {
                    _isDraggingJoint = true;
                    _dragOffset = SelectedJoint.Position - worldPos;
                }
            }
            else if (CurrentTool == RiggingToolMode.EditMesh)
            {
                VertexModel closestVertex = FindClosestVertex(worldPos, clickRadiusSq);

                if (isCtrlPressed)
                {
                    if (closestVertex != null)
                    {
                        if (VertexSelectionForTriangle.Contains(closestVertex))
                        {
                            VertexSelectionForTriangle.Remove(closestVertex);
                        }
                        else
                        {
                            VertexSelectionForTriangle.Add(closestVertex);
                        }
                        SelectedVertex = closestVertex;
                        if (VertexSelectionForTriangle.Count == 3)
                        {
                            var v1 = VertexSelectionForTriangle[0];
                            var v2 = VertexSelectionForTriangle[1];
                            var v3 = VertexSelectionForTriangle[2];
                            if (!TriangleExists(v1, v2, v3))
                            {
                                Triangles.Add(new TriangleModel(v1, v2, v3));
                                SaveRigCommand.NotifyCanExecuteChanged();
                            }
                            VertexSelectionForTriangle.Clear();
                            SelectedVertex = null;
                        }
                    }
                }
                else
                {
                    VertexSelectionForTriangle.Clear();
                    if (closestVertex != null)
                    {
                        SelectedVertex = closestVertex;
                        _isDraggingVertex = true;
                        _vertexDragOffset = SelectedVertex.BindPosition - worldPos;
                    }
                    else
                    {
                        var newVertex = new VertexModel(_vertexIdCounter++, worldPos);
                        AddVertex(newVertex); // DƏYİŞİKLİK
                        SelectedVertex = newVertex;
                        _isDraggingVertex = true;
                        _vertexDragOffset = SKPoint.Empty;
                        SaveRigCommand.NotifyCanExecuteChanged();
                    }
                }
            }

            RequestRedraw?.Invoke(this, EventArgs.Empty);
        }

        // === OnCanvasMouseMoved (YENİLƏNMİŞ - DEFORMASIYA ƏLAVƏ EDİLDİ) ===
        public void OnCanvasMouseMoved(SKPoint screenPos, bool isCtrlPressed)
        {
            if (_isPanning)
            {
                SKPoint delta = new SKPoint(screenPos.X - _lastPanPosition.X, screenPos.Y - _lastPanPosition.Y);
                CameraOffset = new SKPoint(CameraOffset.X + delta.X, CameraOffset.Y + delta.Y);
                _lastPanPosition = screenPos;
                RequestRedraw?.Invoke(this, EventArgs.Empty);
                return;
            }

            SKPoint worldPos = ScreenToWorld(screenPos);
            CurrentMousePosition = worldPos;

            if (isCtrlPressed)
            {
                _isDraggingJoint = false;
                _isDraggingVertex = false;
            }

            if (_isDraggingJoint && SelectedJoint != null)
            {
                if (CurrentTool == RiggingToolMode.Edit)
                {
                    // Edit rejimində "sakit" vəziyyəti (Bind Pose) redaktə edirik
                    SelectedJoint.Position = worldPos + _dragOffset;
                    RecomputeBoneParamsFromPositions(); // "Sakit" bucaq/uzunluqları yenilə
                }
                else if (CurrentTool == RiggingToolMode.Pose)
                {
                    if (SelectedJoint.Parent == null)
                    {
                        SKPoint newJointPos = worldPos + _dragOffset;
                        SKPoint delta = newJointPos - SelectedJoint.Position;
                        ApplyRecursiveMove(SelectedJoint, delta);
                    }
                    else
                    {
                        SKPoint parentPos = SelectedJoint.Parent.Position;
                        float newRotation = MathF.Atan2(worldPos.Y - parentPos.Y, worldPos.X - parentPos.X);
                        float rotationDelta = newRotation - SelectedJoint.Rotation;
                        UpdatePoseHierarchy(SelectedJoint, rotationDelta);
                    }

                    // === YENİ (PLAN 3 - FINAL): DEFORMASIYANI ÇAĞIR ===
                    DeformMesh();
                    // ===============================================
                }
                RequestRedraw?.Invoke(this, EventArgs.Empty);
            }
            else if (_isDraggingVertex && SelectedVertex != null && CurrentTool == RiggingToolMode.EditMesh)
            {
                // EditMesh rejimində biz BindPosition-u (əsas mövqe) redaktə edirik
                SKPoint newVertexPos = worldPos + _vertexDragOffset;
                SelectedVertex.BindPosition = newVertexPos;
                SelectedVertex.CurrentPosition = newVertexPos; // Sakit vəziyyətdə bərabərdirlər
                RequestRedraw?.Invoke(this, EventArgs.Empty);
            }
            else if (CurrentTool == RiggingToolMode.CreateJoint && SelectedJoint != null)
            {
                RequestRedraw?.Invoke(this, EventArgs.Empty);
            }
        }

        #region Find Helpers
        private JointModel FindClosestJoint(SKPoint worldPos, float radiusSq)
        {
            JointModel closestJoint = null;
            float minDistanceSq = radiusSq;
            foreach (var joint in Joints)
            {
                float dx = worldPos.X - joint.Position.X;
                float dy = worldPos.Y - joint.Position.Y;
                float distanceSq = (dx * dx) + (dy * dy);
                if (distanceSq < minDistanceSq)
                {
                    minDistanceSq = distanceSq;
                    closestJoint = joint;
                }
            }
            return closestJoint;
        }
        private VertexModel FindClosestVertex(SKPoint worldPos, float radiusSq)
        {
            VertexModel closestVertex = null;
            float minDistanceSq = radiusSq;
            foreach (var vertex in Vertices)
            {
                float dx = worldPos.X - vertex.BindPosition.X;
                float dy = worldPos.Y - vertex.BindPosition.Y;
                float distanceSq = (dx * dx) + (dy * dy);
                if (distanceSq < minDistanceSq)
                {
                    minDistanceSq = distanceSq;
                    closestVertex = vertex;
                }
            }
            return closestVertex;
        }
        private bool TriangleExists(VertexModel v1, VertexModel v2, VertexModel v3)
        {
            var idSet = new HashSet<int> { v1.Id, v2.Id, v3.Id };
            foreach (var triangle in Triangles)
            {
                var existingSet = new HashSet<int> { triangle.V1.Id, triangle.V2.Id, triangle.V3.Id };
                if (idSet.SetEquals(existingSet))
                {
                    return true;
                }
            }
            return false;
        }
        #endregion

        #region Rigging Helpers
        public void RecomputeBoneParamsFromPositions()
        {
            foreach (var j in Joints)
            {
                if (j.Parent == null)
                {
                    j.BoneLength = 0f;
                    j.Rotation = 0f;
                    continue;
                }
                float dx = j.Position.X - j.Parent.Position.X;
                float dy = j.Position.Y - j.Parent.Position.Y;
                float len = MathF.Sqrt(dx * dx + dy * dy);
                j.BoneLength = (len < EPS) ? 0f : len;
                j.Rotation = (len < EPS) ? 0f : MathF.Atan2(dy, dx);
            }
        }
        #endregion

        #region Deformation
        private void ApplyRecursiveMove(JointModel joint, SKPoint delta)
        {
            if (joint == null) return;
            joint.Position += delta;
            foreach (var child in Joints.Where(j => j.Parent == joint).ToList())
            {
                ApplyRecursiveMove(child, delta);
            }
        }
        private void UpdatePoseHierarchy(JointModel joint, float rotationDelta)
        {
            if (joint == null) return;
            joint.Rotation += rotationDelta;
            if (joint.Parent != null)
            {
                if (joint.BoneLength > EPS)
                {
                    joint.Position = new SKPoint(
                        joint.Parent.Position.X + MathF.Cos(joint.Rotation) * joint.BoneLength,
                        joint.Parent.Position.Y + MathF.Sin(joint.Rotation) * joint.BoneLength
                    );
                }
                else
                {
                    joint.Position = joint.Parent.Position;
                }
            }
            foreach (var child in Joints.Where(j => j.Parent == joint).ToList())
            {
                UpdatePoseHierarchy(child, rotationDelta);
            }
        }

        // === YENİ (PLAN 3 - FINAL): ƏSAS DEFORMASİYA METODU (XƏTA DÜZƏLDİLİB) ===
        private void DeformMesh()
        {
            var jointMap = Joints.ToDictionary(j => j.Id);

            foreach (var vertex in Vertices)
            {
                SKPoint finalDeformedPos = SKPoint.Empty;
                float totalWeight = 0;

                foreach (var weight in vertex.Weights)
                {
                    int jointId = weight.Key;
                    float w = weight.Value;

                    if (!jointMap.TryGetValue(jointId, out var joint) ||
                        !_jointBindPositions.TryGetValue(jointId, out var bindPos) ||
                        !_jointBindRotations.TryGetValue(jointId, out var bindRot))
                    {
                        continue;
                    }

                    SKPoint posedPos = joint.Position;
                    float posedRot = joint.Rotation;

                    // 1. Nöqtənin sümüyün "sakit" mövqeyinə görə nisbi yerini tap
                    SKPoint vRel = vertex.BindPosition - bindPos;

                    // 2. Sümüyün nə qədər fırlandığını tap (hərəkətli - sakit)
                    float deltaRot = posedRot - bindRot;

                    // 3. Nöqtəni həmin bucaq qədər fırlat
                    float cos = MathF.Cos(deltaRot);
                    float sin = MathF.Sin(deltaRot);
                    float rotatedX = vRel.X * cos - vRel.Y * sin;
                    float rotatedY = vRel.X * sin + vRel.Y * cos;

                    // 4. Fırlanmış nöqtəni sümüyün yeni ("hərəkətli") mövqeyinə əlavə et
                    SKPoint vTransformed = new SKPoint(rotatedX, rotatedY) + posedPos;

                    // 5. Yekun mövqeyə ağırlıq dərəcəsində əlavə et
                    // === DÜZƏLİŞ 1 (Operator *) ===
                    finalDeformedPos += new SKPoint(vTransformed.X * w, vTransformed.Y * w);
                    // =============================
                    totalWeight += w;
                }

                // 6. Bütün təsirlərin ortalamasını al
                if (totalWeight > EPS)
                {
                    // === DÜZƏLİŞ 2 (Operator /) ===
                    vertex.CurrentPosition = new SKPoint(finalDeformedPos.X / totalWeight, finalDeformedPos.Y / totalWeight);
                    // =============================
                }
                else
                {
                    vertex.CurrentPosition = vertex.BindPosition;
                }
            }
        }
        // ========================================================

        #endregion


        public void OnCanvasLeftReleased()
        {
            _isDraggingJoint = false;
            _isDraggingVertex = false;
        }

        public void DeselectCurrentJoint()
        {
            if (SelectedJoint != null)
            {
                SelectedJoint = null;
                RequestRedraw?.Invoke(this, EventArgs.Empty);
            }
        }
        public void DeselectCurrentVertex()
        {
            if (SelectedVertex != null)
            {
                SelectedVertex = null;
            }
            if (VertexSelectionForTriangle.Count > 0)
            {
                VertexSelectionForTriangle.Clear();
            }
            RequestRedraw?.Invoke(this, EventArgs.Empty);
        }

        public void DeleteSelectedJoint()
        {
            if (SelectedJoint == null) return;
            var jointToRemove = SelectedJoint;
            SelectedJoint = null;
            _isDraggingJoint = false;
            RemoveJoint(jointToRemove); // DƏYİŞİKLİK
            foreach (var joint in Joints.ToList())
            {
                if (joint.Parent == jointToRemove)
                {
                    joint.Parent = null;
                    joint.BoneLength = 0;
                    joint.Rotation = 0;
                }
            }
            foreach (var vertex in Vertices)
            {
                if (vertex.Weights.ContainsKey(jointToRemove.Id))
                {
                    vertex.Weights.Remove(jointToRemove.Id);
                }
            }
            RequestRedraw?.Invoke(this, EventArgs.Empty);
            SaveRigCommand.NotifyCanExecuteChanged();
        }

        public void DeleteSelectedVertex()
        {
            if (SelectedVertex == null) return;
            var vertexToRemove = SelectedVertex;
            SelectedVertex = null;
            _isDraggingVertex = false;
            RemoveVertex(vertexToRemove); // DƏYİŞİKLİK
            if (VertexSelectionForTriangle.Contains(vertexToRemove))
            {
                VertexSelectionForTriangle.Remove(vertexToRemove);
            }
            var trianglesToRemove = Triangles.Where(t =>
                t.V1 == vertexToRemove ||
                t.V2 == vertexToRemove ||
                t.V3 == vertexToRemove)
                .ToList();
            foreach (var triangle in trianglesToRemove)
            {
                Triangles.Remove(triangle);
            }
            RequestRedraw?.Invoke(this, EventArgs.Empty);
            SaveRigCommand.NotifyCanExecuteChanged();
        }


        private void ClearRiggingData()
        {
            Joints.Clear();
            SelectedJoint = null;
            _jointIdCounter = 0;
            Vertices.Clear();
            Triangles.Clear();
            SelectedVertex = null;
            VertexSelectionForTriangle.Clear();
            _vertexIdCounter = 0;

            _jointBindPositions.Clear();
            _jointBindRotations.Clear();

            SaveRigCommand?.NotifyCanExecuteChanged();
            AutoWeightCommand?.NotifyCanExecuteChanged();
        }

        // === YENİLƏNMİŞ (PLAN 3 - FINAL): ALƏT DƏYİŞMƏ MƏNTİQİ ===
        partial void OnCurrentToolChanged(RiggingToolMode value)
        {
            // Alət dəyişəndə köhnə vəziyyəti təmizlə
            if (_jointBindPositions.Count > 0)
            {
                ResetPoseToBindPose(); // Pose rejimindən çıxırıqsa, hər şeyi sıfırla
            }

            // Yeni rejimə keç
            if (value == RiggingToolMode.Pose)
            {
                StoreBindPose(); // Pose rejiminə giririksə, "sakit" vəziyyəti yadda saxla
            }

            // Bütün seçimləri ləğv et
            SelectedJoint = null;
            SelectedVertex = null;
            VertexSelectionForTriangle.Clear();
            RequestRedraw?.Invoke(this, EventArgs.Empty);

            _isDraggingJoint = false;
            _isDraggingVertex = false;

            AutoWeightCommand.NotifyCanExecuteChanged();
        }

        // === YENİ (PLAN 3 - FINAL): "SAKİT VƏZİYYƏT" METODLARI ===
        private void StoreBindPose()
        {
            // 1. Sümüklərin "sakit" bucaqlarını və uzunluqlarını hesabla
            RecomputeBoneParamsFromPositions();

            _jointBindPositions.Clear();
            _jointBindRotations.Clear();

            // 2. Həmin "sakit" vəziyyəti yadda saxla
            foreach (var joint in Joints)
            {
                _jointBindPositions[joint.Id] = joint.Position;
                _jointBindRotations[joint.Id] = joint.Rotation;
            }
        }

        private void ResetPoseToBindPose()
        {
            if (_jointBindPositions.Count == 0) return; // Artıq sıfırlanıbsa

            // Sümükləri "sakit" vəziyyətinə qaytar
            foreach (var joint in Joints)
            {
                if (_jointBindPositions.TryGetValue(joint.Id, out var bindPos))
                {
                    joint.Position = bindPos;
                }
            }

            // Nöqtələri (Vertices) "sakit" vəziyyətinə qaytar
            foreach (var vertex in Vertices)
            {
                vertex.CurrentPosition = vertex.BindPosition;
            }

            // Yaddaşı təmizlə
            _jointBindPositions.Clear();
            _jointBindRotations.Clear();
        }

        // === YENİ: Siyahı dəyişikliklərini izləmək üçün köməkçi metodlar ===
        private void AddJoint(JointModel newJoint)
        {
            Joints.Add(newJoint);
            AutoWeightCommand.NotifyCanExecuteChanged();
            AutoTriangleCommand.NotifyCanExecuteChanged();
            //AutoGenerateVerticesCommand.NotifyCanExecuteChanged(); // <-- əlavə et
        }
        private void AddVertex(VertexModel newVertex)
        {
            Vertices.Add(newVertex);
            AutoWeightCommand.NotifyCanExecuteChanged();
            AutoTriangleCommand.NotifyCanExecuteChanged();
        }
        private void RemoveJoint(JointModel jointToRemove)
        {
            Joints.Remove(jointToRemove);
            AutoWeightCommand.NotifyCanExecuteChanged();
            AutoTriangleCommand.NotifyCanExecuteChanged();
            //AutoGenerateVerticesCommand.NotifyCanExecuteChanged(); // <-- əlavə et
        }
        private void RemoveVertex(VertexModel vertexToRemove)
        {
            Vertices.Remove(vertexToRemove);
            AutoWeightCommand.NotifyCanExecuteChanged();
            AutoTriangleCommand.NotifyCanExecuteChanged();
        }




        //// === YENİ (AVTO-TRİANGULATE - DÜZƏLDİLMİŞ) ===
        //[RelayCommand(CanExecute = nameof(CanAutoTriangle))]
        //private void AutoTriangle()
        //{
        //    if (!CanAutoTriangle()) return;

        //    // 1) Köhnə üçbucaqları təmizlə
        //    Triangles.Clear();

        //    // 2) Triangle.NET üçün Polygon hazırla
        //    var polygon = new TriangleNet.Geometry.Polygon();

        //    // 3) ID -> VertexModel xəritəsi (SABİT HƏLL)
        //    var idToVm = new Dictionary<int, VertexModel>(Vertices.Count);

        //    // NOTE: Triangle.NET-in Vertex.ID sahəsini mütləq bizim VertexModel.Id ilə eyniləşdiririk
        //    foreach (var vmVertex in Vertices)
        //    {
        //        var tnVertex = new TriangleNet.Geometry.Vertex(vmVertex.BindPosition.X, vmVertex.BindPosition.Y)
        //        {
        //            ID = vmVertex.Id
        //        };
        //        polygon.Add(tnVertex);
        //        idToVm[vmVertex.Id] = vmVertex;
        //    }

        //    // 4) Triangulate
        //    var mesh = (TriangleNet.Mesh)polygon.Triangulate();

        //    // 5) Triangle.NET nəticələrini öz TriangleModel-lərimizə çevir
        //    //    Burada artıq referansla yox, ID ilə map edirik — stabil işləyir
        //    int created = 0;
        //    foreach (var tnTriangle in mesh.Triangles)
        //    {
        //        var v0 = tnTriangle.GetVertex(0);
        //        var v1 = tnTriangle.GetVertex(1);
        //        var v2 = tnTriangle.GetVertex(2);

        //        // ID-lərdən bizim VertexModel-ləri götür
        //        if (!idToVm.TryGetValue(v0.ID, out var vmV0)) continue;
        //        if (!idToVm.TryGetValue(v1.ID, out var vmV1)) continue;
        //        if (!idToVm.TryGetValue(v2.ID, out var vmV2)) continue;

        //        // Mümkünsə təkrarı yoxla (opsional)
        //        if (!TriangleExists(vmV0, vmV1, vmV2))
        //        {
        //            Triangles.Add(new TriangleModel(vmV0, vmV1, vmV2));
        //            created++;
        //        }
        //    }

        //    // 6) UI və command-ları yenilə
        //    SaveRigCommand.NotifyCanExecuteChanged();
        //    AutoTriangleCommand.NotifyCanExecuteChanged();
        //    RequestRedraw?.Invoke(this, EventArgs.Empty);

    //        CustomMessageBox.Show(
    //    App.GetStr("Str_Msg_AutoTriResult", created), 
    //"Str_Title_Success", 
    //MessageBoxButton.OK, 
    //MsgImage.Success);
        //}


        // Fayl: ViewModels/RiggingViewModel.cs



        // 1) Triangle.NET CDT helper
        private (List<SKPoint> verts, List<(int a, int b, int c)> tris)
        TriangulateWithConstraints(
            IList<SKPoint> contour,
            IList<SKPoint> interior,
            double minAngleDeg,
            double targetEdgeLen)
        {
            // polygon + contour segments
            var poly = new Polygon(contour.Count);
            var outer = new Contour(contour.Select(p => new Vertex(p.X, p.Y)), 0, true);
            poly.Add(outer); // kənar bütün kənarları "segment" kimi əlavə olunur

            // daxili nöqtələr (Steiner)
            if (interior != null)
                foreach (var p in interior) poly.Add(new Vertex(p.X, p.Y));

            // daha sıx mesh üçün sahəni bir az da kiçik edirik
            double maxArea = Math.Max(1.5, targetEdgeLen * targetEdgeLen * 0.35);

            var co = new ConstraintOptions { ConformingDelaunay = true };
            var qo = new QualityOptions
            {
                MinimumAngle = minAngleDeg,
                MaximumArea = maxArea
            };

            var mesh = (TriangleNet.Mesh)poly.Triangulate(co, qo);

            // çıxış
            var outVerts = mesh.Vertices.Select(v => new SKPoint((float)v.X, (float)v.Y)).ToList();
            var outTris = new List<(int, int, int)>();
            foreach (var t in mesh.Triangles)
            {
                var a = t.GetVertex(0)?.ID ?? -1;
                var b = t.GetVertex(1)?.ID ?? -1;
                var c = t.GetVertex(2)?.ID ?? -1;
                if (a >= 0 && b >= 0 && c >= 0)
                    outTris.Add((a, b, c));
            }

            return (outVerts, outTris);
        }


   





        // === DÜZƏLİŞ: AutoTriangle (Sadə və Stabil Versiya) ===

        [RelayCommand(CanExecute = nameof(CanAutoTriangle))]
        private void AutoTriangle()
        {
            if (!CanAutoTriangle()) return;

            // 1) Köhnə üçbucaqları təmizlə
            Triangles.Clear();

            // 2) Triangle.NET üçün Polygon hazırla
            var polygon = new TriangleNet.Geometry.Polygon();

            // 3) Bütün mövcud VertexModel-ləri ID-lərinə görə xəritəyə salaq
            var idToVm = new Dictionary<int, VertexModel>(Vertices.Count);

            // 4) Hər bir VertexModel-i Triangle.NET-in Vertex-inə çevir
            // Vacib: Bizim VertexModel.Id-ni Triangle.NET-in Vertex.ID-sinə mənimsədirik
            foreach (var vmVertex in Vertices)
            {
                var tnVertex = new TriangleNet.Geometry.Vertex(vmVertex.BindPosition.X, vmVertex.BindPosition.Y)
                {
                    ID = vmVertex.Id
                };
                polygon.Add(tnVertex);

                // Xəritəyə əlavə et
                if (!idToVm.ContainsKey(vmVertex.Id))
                {
                    idToVm.Add(vmVertex.Id, vmVertex);
                }
            }

            // 5) SADƏ Triangulyasiya et (Constraint və ya Quality OLMADAN)
            // Bu metod YALNIZ verilən nöqtələrdən istifadə edəcək, yenilərini yaratmayacaq.
            try
            {
                var mesh = (TriangleNet.Mesh)polygon.Triangulate();

                // 6) Nəticəni öz TriangleModel-lərimizə çevir
                int created = 0;
                foreach (var tnTriangle in mesh.Triangles)
                {
                    // Nöqtələri ID ilə götürürük
                    var v0 = tnTriangle.GetVertex(0);
                    var v1 = tnTriangle.GetVertex(1);
                    var v2 = tnTriangle.GetVertex(2);

                    // ID-lərə görə bizim VertexModel-ləri tapırıq
                    if (v0 == null || v1 == null || v2 == null) continue;

                    if (!idToVm.TryGetValue(v0.ID, out var vmV0)) continue;
                    if (!idToVm.TryGetValue(v1.ID, out var vmV1)) continue;
                    if (!idToVm.TryGetValue(v2.ID, out var vmV2)) continue;

                    // Təkrar yoxla (opsional, amma faydalıdır)
                    if (!TriangleExists(vmV0, vmV1, vmV2))
                    {
                        Triangles.Add(new TriangleModel(vmV0, vmV1, vmV2));
                        created++;
                    }
                }

                // 7) UI və command-ları yenilə
                SaveRigCommand.NotifyCanExecuteChanged();
                RequestRedraw?.Invoke(this, EventArgs.Empty);

                MessageBox.Show($"{created} üçbucaq avtomatik yaradıldı.", "Uğurlu");
            }
            catch (Exception ex)
            {
                // Triangle.NET bəzən uğursuz ola bilir (məs. bütün nöqtələr bir xətt üzrədirsə)
                MessageBox.Show($"Triangulyasiya zamanı xəta: {ex.Message}\n\nNöqtələrin düzgün yerləşdiyindən əmin olun.", "Xəta");
            }
        }

        private bool CanAutoTriangle() => Vertices.Count >= 3;

        // Yuxarıdakı metod üçün ComputeConvexHull, MapCdtOutputToVm, 
        // TriangulateWithConstraints, PointLineDist2 və s. köməkçi metodlara ehtiyac yoxdur.
        // Onları silə və ya saxlaya bilərsiniz.
        // =============== HELPERS ===============

        // Monotone chain (O(n log n)) – VertexModel üzrə konveks hull qaytarır
 

        private static bool HasEdgeLongerThan(VertexModel a, VertexModel b, VertexModel c, float cap)
        {
            float dAB = Dist(a.BindPosition, b.BindPosition);
            float dBC = Dist(b.BindPosition, c.BindPosition);
            float dCA = Dist(c.BindPosition, a.BindPosition);
            return (dAB > cap) || (dBC > cap) || (dCA > cap);
        }

        private static float Dist(SKPoint p, SKPoint q)
        {
            float dx = p.X - q.X, dy = p.Y - q.Y;
            return MathF.Sqrt(dx * dx + dy * dy);
        }




        private void AutoGenerateVertices()
        {
            if (!CanAutoGenVertices()) return;

            Vertices.Clear();
            Triangles.Clear();
            _vertexIdCounter = 0;

            int minDim = Math.Min(LoadedBitmap.Width, LoadedBitmap.Height);

            // 3.1) Düzgün kontur (marching squares → CCW)
            var mask = BuildMaskFromAlphaOrBg(LoadedBitmap, alphaTh: 8, bgDelta: 18);

            // YENİ: maskadan kontur çək
            int step = Math.Clamp(minDim / 180, 1, 4);
            var contour = TraceOuterContourFromMask(mask, step);
            if (contour.Count < 3)
            {
                MessageBox.Show("Kontur çıxarıla bilmədi (alfa və ya fon seçimi uğursuz). 'Background Eraser' istifadə et və ya bgDelta-ni artır.", "Xəta");
                return;
            }

            // 3.2) Poisson disk ilə daxili nümunə — hədəf kənar uzunluğuna bağlı sıxlıq
            float targetEdgeLen = MathF.Max(2.5f, minDim / 140f);
            var interior = PoissonDiskInPolygon(contour, radius: targetEdgeLen);

            // 3.3) CDT – Conforming Delaunay + sahə limitini bir az aqressiv saxla
            var (cdtVerts, cdtTris) = TriangulateWithConstraints(contour, interior, minAngleDeg: 28.0, targetEdgeLen);

            // 3.4) CDT çıxışını **birbaşa** VM-ə KÖÇÜR (SNAP YOXDUR!)
            var idx2Vm = new List<VertexModel>(cdtVerts.Count);
            foreach (var p in cdtVerts)
            {
                var vm = new VertexModel(_vertexIdCounter++, p);
                Vertices.Add(vm);
                idx2Vm.Add(vm);
            }

            // 3.5) Uzun kənarları at (artefaktlara qarşı)
            float longCap = targetEdgeLen * 3.0f;

            int added = 0;
            foreach (var (a, b, c) in cdtTris)
            {
                var vA = idx2Vm[a];
                var vB = idx2Vm[b];
                var vC = idx2Vm[c];

                if (HasEdgeLongerThan(vA, vB, vC, longCap)) continue;

                if (!TriangleExists(vA, vB, vC))
                {
                    Triangles.Add(new TriangleModel(vA, vB, vC));
                    added++;
                }
            }

            SaveRigCommand.NotifyCanExecuteChanged();
            AutoWeightCommand.NotifyCanExecuteChanged();
            RequestRedraw?.Invoke(this, EventArgs.Empty);

            MessageBox.Show($"AutoMesh: {Vertices.Count} vertex, {Triangles.Count} üçbucaq.", "Uğurlu");
        }



        private bool CanAutoGenVertices()
        {
            return IsImageLoaded && Joints.Count >= 2;
        }
        private static List<SKPoint> DedupPoints(List<SKPoint> pts, float tol)
        {
            float tol2 = tol * tol;
            var outList = new List<SKPoint>(pts.Count);

            foreach (var p in pts)
            {
                bool exists = false;
                for (int i = 0; i < outList.Count; i++)
                {
                    float dx = p.X - outList[i].X, dy = p.Y - outList[i].Y;
                    if (dx * dx + dy * dy <= tol2) { exists = true; break; }
                }
                if (!exists) outList.Add(p);
            }
            return outList;
        }



        private static bool PointInPolygon(IList<SKPoint> poly, SKPoint p)
        {
            bool inside = false;
            for (int i = 0, j = poly.Count - 1; i < poly.Count; j = i++)
            {
                var pi = poly[i]; var pj = poly[j];
                bool intersect = ((pi.Y > p.Y) != (pj.Y > p.Y)) &&
                                 (p.X < (pj.X - pi.X) * (p.Y - pi.Y) / Math.Max(1e-6f, (pj.Y - pi.Y)) + pi.X);
                if (intersect) inside = !inside;
            }
            return inside;
        }



        // 1B) Kiçik RDP + CCW
        private List<SKPoint> DedupPointsRdp(List<SKPoint> pts, float mergeTol, float rdpTol, bool ensureCcw)
        {
            if (pts == null || pts.Count < 3) return pts ?? new List<SKPoint>();

            // birləşdirmə
            var merged = DedupPoints(pts, mergeTol);

            // RDP
            float Tol2 = rdpTol * rdpTol;
            var outL = new List<SKPoint>();
            void Rdp(int a, int b)
            {
                float maxD = 0f; int idx = -1;
                var A = merged[a]; var B = merged[b];
                for (int i = a + 1; i < b; i++)
                {
                    float d = PointLineDist2(merged[i], A, B);
                    if (d > maxD) { maxD = d; idx = i; }
                }
                if (idx != -1 && maxD > Tol2)
                {
                    Rdp(a, idx); Rdp(idx, b);
                }
                else outL.Add(A);
            }
            Rdp(0, merged.Count - 1); outL.Add(merged[^1]);

            if (ensureCcw)
            {
                float area2 = 0f;
                for (int i = 0; i < outL.Count; i++)
                {
                    var a = outL[i]; var b = outL[(i + 1) % outL.Count];
                    area2 += a.X * b.Y - a.Y * b.X;
                }
                if (area2 < 0) outL.Reverse();
            }
            return outL;
        }

        private float PointLineDist2(SKPoint p, SKPoint a, SKPoint b)
        {
            float vx = b.X - a.X, vy = b.Y - a.Y;
            float wx = p.X - a.X, wy = p.Y - a.Y;
            float t = (vx * wx + vy * wy) / Math.Max(1e-6f, vx * vx + vy * vy);
            t = MathF.Max(0, MathF.Min(1, t));
            float dx = a.X + t * vx - p.X, dy = a.Y + t * vy - p.Y;
            return dx * dx + dy * dy;
        }

        // radius ~ hədəf kənar uzunluğu, bbox + rejection sampling + grid sürətləndirici
        private List<SKPoint> PoissonDiskInPolygon(IList<SKPoint> poly, float radius, int k = 30)
        {
            var rnd = new Random(12345);
            float r = Math.Max(1.0f, radius);
            float cell = r / MathF.Sqrt(2f);

            // bbox
            float minX = poly.Min(p => p.X), minY = poly.Min(p => p.Y);
            float maxX = poly.Max(p => p.X), maxY = poly.Max(p => p.Y);
            int gx = Math.Max(1, (int)MathF.Ceiling((maxX - minX) / cell));
            int gy = Math.Max(1, (int)MathF.Ceiling((maxY - minY) / cell));

            var grid = new int[gx * gy];
            Array.Fill(grid, -1);

            var samples = new List<SKPoint>();
            var active = new List<int>();

            SKPoint RandPoint() =>
                new SKPoint((float)(minX + rnd.NextDouble() * (maxX - minX)),
                            (float)(minY + rnd.NextDouble() * (maxY - minY)));

            bool InPoly(SKPoint p) => PointInPolygon(poly, p);

            bool FarEnough(SKPoint p)
            {
                int ix = (int)((p.X - minX) / cell);
                int iy = (int)((p.Y - minY) / cell);
                for (int yy = Math.Max(0, iy - 2); yy <= Math.Min(gy - 1, iy + 2); yy++)
                    for (int xx = Math.Max(0, ix - 2); xx <= Math.Min(gx - 1, xx + 2); xx++)
                    {
                        int id = grid[yy * gx + xx];
                        if (id < 0) continue;
                        var q = samples[id];
                        float dx = p.X - q.X, dy = p.Y - q.Y;
                        if (dx * dx + dy * dy < r * r) return false;
                    }
                return true;
            }

            // ilk nümunə
            for (int tries = 0; tries < 1000 && samples.Count == 0; tries++)
            {
                var p = RandPoint();
                if (!InPoly(p)) continue;
                samples.Add(p); active.Add(0);
                int ix = (int)((p.X - minX) / cell), iy = (int)((p.Y - minY) / cell);
                grid[iy * gx + ix] = 0;
            }
            // genişlət
            while (active.Count > 0 && samples.Count < 20000)
            {
                int ai = active[rnd.Next(active.Count)];
                var baseP = samples[ai];
                bool found = false;
                for (int i = 0; i < k; i++)
                {
                    float ang = (float)(rnd.NextDouble() * Math.PI * 2);
                    float rad = r * (1f + (float)rnd.NextDouble());
                    var cand = new SKPoint(baseP.X + rad * MathF.Cos(ang), baseP.Y + rad * MathF.Sin(ang));
                    if (!InPoly(cand) || !FarEnough(cand)) continue;

                    samples.Add(cand);
                    active.Add(samples.Count - 1);
                    int ix = (int)((cand.X - minX) / cell), iy = (int)((cand.Y - minY) / cell);
                    if (ix >= 0 && iy >= 0 && ix < gx && iy < gy) grid[iy * gx + ix] = samples.Count - 1;
                    found = true; break;
                }
                if (!found) active.Remove(ai);
            }
            return samples;
        }


        // Fon rəngini künclərdən götürürük və ondan “kifayət qədər fərqli” pikselləri obyekt sayırıq.
        private bool[,] BuildMaskFromAlphaOrBg(SKBitmap bmp, byte alphaTh = 8, int bgDelta = 80)
        {
            int W = bmp.Width, H = bmp.Height;
            var mask = new bool[W, H];

            // Alpha varmı? (yəni şəkildə əhəmiyyətli sayda piksel alphaTh-dan böyükdür)
            int alphaCount = 0, total = Math.Max(1, W * H / 50); // sürət üçün təxmini yoxlama
            for (int y = 0; y < H; y += Math.Max(1, H / 50))
            {
                for (int x = 0; x < W; x += Math.Max(1, W / 50))
                {
                    if (bmp.GetPixel(x, y).Alpha > alphaTh) alphaCount++;
                }
            }
           // Əgər nümunədə həm şəffaf, həm qeyri-şəffaf piksel varsa, deməli "hasAlpha" doğrudur.
             bool hasAlpha = (alphaCount > 0) && (alphaCount < total);

            if (hasAlpha)
            {
                for (int y = 0; y < H; y++)
                    for (int x = 0; x < W; x++)
                        mask[x, y] = bmp.GetPixel(x, y).Alpha > alphaTh;
                return mask;
            }

            // Alfa yoxdursa: fon rəngi = 4 küncün medianı
            SKColor[] corners = new[]
            {
                 bmp.GetPixel(1,1),
                 bmp.GetPixel(W-2,1),
                 bmp.GetPixel(1,H-2),
                 bmp.GetPixel(W-2,H-2)
    };
            byte mr = (byte)corners.Select(c => c.Red).OrderBy(v => v).ElementAt(2);
            byte mg = (byte)corners.Select(c => c.Green).OrderBy(v => v).ElementAt(2);
            byte mb = (byte)corners.Select(c => c.Blue).OrderBy(v => v).ElementAt(2);

            int thr2 = bgDelta * bgDelta;
            for (int y = 0; y < H; y++)
            {
                for (int x = 0; x < W; x++)
                {
                    var c = bmp.GetPixel(x, y);
                    int dr = c.Red - mr;
                    int dg = c.Green - mg;
                    int db = c.Blue - mb;
                    int d2 = dr * dr + dg * dg + db * db;
                    mask[x, y] = d2 > thr2; // fondan xeyli fərqlənirsə, obyekt say
                }
            }

            using (var tmp = new SKBitmap(bmp.Width, bmp.Height))
            {
                for (int y = 0; y < bmp.Height; y++)
                    for (int x = 0; x < bmp.Width; x++)
                        tmp.SetPixel(x, y, mask[x, y] ? new SKColor(0, 255, 0) : new SKColor(40, 40, 40));
                using var image = SKImage.FromBitmap(tmp);
                using var data = image.Encode(SKEncodedImageFormat.Png, 100);
                File.WriteAllBytes("mask_debug.png", data.ToArray());
            }

            return mask;
        }


        // Maskadakı “true” regionun kənarını CCW qaytarır
        private List<SKPoint> TraceOuterContourFromMask(bool[,] mask, int step = 1)
        {
            int W = mask.GetLength(0), H = mask.GetLength(1);

            SKPoint? start = null;
            for (int y = 1; y < H - 1 && start == null; y += step)
                for (int x = 1; x < W - 1; x += step)
                    if (mask[x, y]) { start = new SKPoint(x, y); break; }

            if (start == null) return new List<SKPoint>();

            var contour = new List<SKPoint>(1024);
            int[,] dirs = { { 1, 0 }, { 1, 1 }, { 0, 1 }, { -1, 1 }, { -1, 0 }, { -1, -1 }, { 0, -1 }, { 1, -1 } };

            bool Inside(int x, int y) =>
                x >= 0 && y >= 0 && x < W && y < H && mask[x, y];

            int cx = (int)start.Value.X, cy = (int)start.Value.Y;
            int dir = 0, guard = W * H * 4;

            do
            {
                contour.Add(new SKPoint(cx, cy));
                int best = -1;
                for (int k = 0; k < 8; k++)
                {
                    int i = (dir + k) % 8;
                    int nx = cx + dirs[i, 0], ny = cy + dirs[i, 1];
                    if (Inside(nx, ny)) { best = i; break; }
                }
                if (best == -1) break;
                cx += dirs[best, 0]; cy += dirs[best, 1];
                dir = (best + 7) % 8;
                guard--;
            }
            while (guard > 0 && (Math.Abs(cx - start.Value.X) > 1 || Math.Abs(cy - start.Value.Y) > 1));

            return DedupPointsRdp(contour, 1.5f, 0.75f, ensureCcw: true);
        }


        [RelayCommand]
        private void AddKeyframe()
        {
            if (SelectedJoint == null) return;
            if (CurrentAnimation == null)
            {
                // Əgər animasiya yoxdursa, yenisini yarat
                CurrentAnimation = new AnimationClipData { Name = "Anim_1" };
                Animations.Add(CurrentAnimation);
            }

            float time = (float)CurrentTime;

            // 1. Fırlanma (Rotation) üçün Track tap və ya yarat
            SaveKeyframe(SelectedJoint.Id, "Rotation", SelectedJoint.Rotation, time);

            // 2. Mövqe (Position) üçün Track (X və Y ayrı)
            // Qeyd: Yalnız Root sümük və ya IK üçün Position vacibdir, amma hamısı üçün saxlayaq.
            SaveKeyframe(SelectedJoint.Id, "PosX", SelectedJoint.Position.X, time);
            SaveKeyframe(SelectedJoint.Id, "PosY", SelectedJoint.Position.Y, time);

            CustomMessageBox.Show(
     App.GetStr("Str_Msg_KeyframeAdded", time),
     "Str_Title_Info",
     MessageBoxButton.OK,
     MsgImage.Info);
        }

        private void SaveKeyframe(int jointId, string propName, float value, float time)
        {
            // Mövcud track-i tap
            var track = CurrentAnimation.Tracks.FirstOrDefault(t => t.JointId == jointId && t.PropertyName == propName);
            if (track == null)
            {
                track = new AnimationTrackData { JointId = jointId, PropertyName = propName };
                CurrentAnimation.Tracks.Add(track);
            }

            // Eyni zamanda köhnə keyframe varsa, onu yenilə
            var existingKey = track.Keyframes.FirstOrDefault(k => Math.Abs(k.Time - time) < 0.01f);
            if (existingKey != null)
            {
                existingKey.Value = value;
            }
            else
            {
                track.Keyframes.Add(new KeyframeData { Time = time, Value = value });
                // Zaman üzrə sırala (vacibdir!)
                track.Keyframes.Sort((a, b) => a.Time.CompareTo(b.Time));
            }
        }


        public void ApplyAnimationAtTime(float time)
        {
            if (CurrentAnimation == null) return;

            foreach (var track in CurrentAnimation.Tracks)
            {
                // Həmin sümüyü tap
                var joint = Joints.FirstOrDefault(j => j.Id == track.JointId);
                if (joint == null) continue;

                // İnterpolasiya dəyərini hesabla
                float interpolatedValue = GetInterpolatedValue(track.Keyframes, time);

                // Dəyəri tətbiq et
                switch (track.PropertyName)
                {
                    case "Rotation":
                        joint.Rotation = interpolatedValue;
                        break;
                    case "PosX":
                        joint.Position = new SKPoint(interpolatedValue, joint.Position.Y);
                        break;
                    case "PosY":
                        joint.Position = new SKPoint(joint.Position.X, interpolatedValue);
                        break;
                }
            }

            // ƏN VACİB HİSSƏ: Sümüklər dəyişdi, indi Mesh-i yenidən hesabla!
            // Pose rejimindəki kimi iyerarxiyanı yeniləyirik (valideyn hərəkəti uşağa keçsin)
            // Qeyd: Əgər hər sümüyün Position/Rotation-unu keyframe ediriksə, 
            // UpdatePoseHierarchy lazım olmaya bilər, amma FK üçün bu vacibdir.
            // Burada sadəlik üçün birbaşa DeformMesh çağırırıq.

            if (Vertices.Count > 0)
            {
                // Yuxarıda Pose məntiqində yazdığınız iyerarxiya yeniləməsini bura inteqrasiya etmək lazımdır
                // Amma hələlik sadə deformasiya:
                // DeformMesh() metodunu Public etməlisiniz ki, burdan çağıra bilək.
                // Və ya bu kod daxildədirsə birbaşa:
                DeformMesh();
            }

            RequestRedraw?.Invoke(this, EventArgs.Empty);
        }

        private float GetInterpolatedValue(List<KeyframeData> keys, float time)
        {
            if (keys.Count == 0) return 0;
            if (keys.Count == 1) return keys[0].Value;

            // 1. Zamandan əvvəlki son kadrı tap (Key A)
            var keyA = keys.LastOrDefault(k => k.Time <= time);
            // 2. Zamandan sonrakı ilk kadrı tap (Key B)
            var keyB = keys.FirstOrDefault(k => k.Time > time);

            if (keyA == null) return keyB.Value; // Başlanğıcdan əvvəl
            if (keyB == null) return keyA.Value; // Sondan sonra

            // 3. Lerp (Linear Interpolation)
            float t = (time - keyA.Time) / (keyB.Time - keyA.Time);
            return keyA.Value + (keyB.Value - keyA.Value) * t;
        }


        // === Timer Hadisəsi (Hər 16ms-dən bir işləyir) ===
        private void AnimationTimer_Tick(object? sender, EventArgs e)
        {
            // Zamanı irəli çək
            CurrentTime += 0.016;

            // Əgər sona çatdısa, başa qaytar (Loop)
            if (CurrentTime >= TotalDuration)
            {
                CurrentTime = 0;
            }
            // Qeyd: CurrentTime dəyişəndə 'OnCurrentTimeChanged' metodu (Mərhələ 2-də yazdıq)
            // avtomatik olaraq 'ApplyAnimationAtTime' metodunu çağırır və şəkli yeniləyir.
        }

        // === Pult Əmrləri (Play/Pause/Stop) ===

        [RelayCommand]
        private void Play()
        {
            IsPlaying = true;
            _animationTimer.Start();
        }

        [RelayCommand]
        private void Pause()
        {
            IsPlaying = false;
            _animationTimer.Stop();
        }

        [RelayCommand]
        private void Stop()
        {
            IsPlaying = false;
            _animationTimer.Stop();
            CurrentTime = 0; // Başa sarı
        }


    }
}