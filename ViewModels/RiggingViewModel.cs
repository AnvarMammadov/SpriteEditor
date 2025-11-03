using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using SkiaSharp;
using SpriteEditor.Data; // Yaratdığımız Data modelləri üçün
using System.Text.Json; // JSON Serializasiyası üçün
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
        [NotifyCanExecuteChangedFor(nameof(AutoGenerateVerticesCommand))]
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



        public RiggingViewModel()
        {
            // (İstəyə bağlı) başlanğıc AutoWeight presetləri
            // Bunları istəmirsənsə, silə bilərsən.
            AwSigmaFactor = 0.20f;
            AwRadialPower = 1.0f;
            AwLongPower = 0.5f;
            AwMinKeep = 0.02f;
            AwTopK = 4;
            AwParentBlend = 0.5f;
            AwAncestorDecay = 0.40f;
            AwSmoothIters = 3;
            AwSmoothMu = 0.55f;
            // Kolleksiya dəyişikliklərini izləyək ki, UI-dakı düymələr vaxtında aktivləşsin/deaktivləşsin
            Joints.CollectionChanged += (_, __) =>
            {
                AutoGenerateVerticesCommand?.NotifyCanExecuteChanged();
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
            AutoGenerateVerticesCommand?.NotifyCanExecuteChanged();
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
            if (Triangles.Count > 0 && Vertices.Count > 0)
            {
                var neighbors = BuildVertexNeighbors(Vertices, Triangles);
                for (int it = 0; it < smoothIters; it++)
                {
                    SmoothWeightsOnce(Vertices, neighbors, smoothMu, topK, minKeep);
                }
            }

            SaveRigCommand.NotifyCanExecuteChanged();
            MessageBox.Show("Avtomatik ağırlıqlandırma tamamlandı (segment-əsaslı)!", "Uğurlu");
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
                Filter = "Görüntü Faylları (*.png)|*.png|Bütün Fayllar (*.*)|*.*"
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
                        throw new Exception("Fayl formatı dəstəklənmir və ya fayl zədəlidir.");
                    }
                    IsImageLoaded = true;
                    ClearRiggingData();
                    ResetCamera();
                    RequestCenterCamera?.Invoke(this, EventArgs.Empty);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Şəkli yükləyərkən xəta baş verdi: {ex.Message}", "Xəta");
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

            OpenFileDialog openDialog = new OpenFileDialog
            {
                Filter = "Rig JSON Faylı (*.rig.json)|*.rig.json|Bütün Fayllar (*.*)|*.*",
                Title = "Skelet Məlumatını Yüklə"
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
                    AutoGenerateVerticesCommand.NotifyCanExecuteChanged();

                    MessageBox.Show("Skelet və Mesh uğurla yükləndi.", "Uğurlu");
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Skeleti yükləyərkən xəta baş verdi: {ex.Message}", "Xəta");
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
                Filter = "Rig JSON Faylı (*.rig.json)|*.rig.json|Bütün Fayllar (*.*)|*.*",
                Title = "Skelet Məlumatını Saxla"
            };

            if (saveDialog.ShowDialog() == true)
            {
                try
                {
                    var options = new JsonSerializerOptions { WriteIndented = true };
                    string jsonString = JsonSerializer.Serialize(rigData, options);
                    await File.WriteAllTextAsync(saveDialog.FileName, jsonString);
                    MessageBox.Show("Skelet və Mesh uğurla yadda saxlandı!", "Uğurlu");
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
            AutoGenerateVerticesCommand.NotifyCanExecuteChanged(); // <-- əlavə et
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
            AutoGenerateVerticesCommand.NotifyCanExecuteChanged(); // <-- əlavə et
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

        //    MessageBox.Show($"{created} üçbucaq avtomatik yaradıldı.", "Uğurlu");
        //}


        // Fayl: ViewModels/RiggingViewModel.cs

        [RelayCommand(CanExecute = nameof(CanAutoTriangle))]
        private void AutoTriangle()
        {
            if (!CanAutoTriangle()) return;

            Triangles.Clear();

            // 1) TN vertex-lərini yarat (ID TƏYİN ETMƏ)
            var tnByVm = new Dictionary<VertexModel, TriangleNet.Geometry.Vertex>(Vertices.Count);
            foreach (var vm in Vertices)
                tnByVm[vm] = new TriangleNet.Geometry.Vertex(vm.BindPosition.X, vm.BindPosition.Y);

            // 2) Kənar silueti HULL ilə qur (yalnız hull nöqtələri contour-a düşür)
            var hull = ComputeConvexHull(Vertices); // VertexModel-lərin hull sırası

            var polygon = new TriangleNet.Geometry.Polygon();

            if (hull.Count >= 3)
            {
                // Hull-dakı TN vertex instansları ilə contour qur
                var contour = new TriangleNet.Geometry.Contour(hull.Select(vm => tnByVm[vm]));
                polygon.Add(contour);

                // DAXİLİ nöqtələri ayrıca əlavə et (hull-da olmayanlar)
                foreach (var vm in Vertices)
                    if (!hull.Contains(vm))
                        polygon.Add(tnByVm[vm]);
            }
            else
            {
                // Hull alınmadısa: sadə fallback – bütün nöqtələri point set kimi əlavə et
                foreach (var vm in Vertices) polygon.Add(tnByVm[vm]);
            }

            // 3) Triangulate (keyfiyyət)
            var copt = new TriangleNet.Meshing.ConstraintOptions { ConformingDelaunay = true };
            var qopt = new TriangleNet.Meshing.QualityOptions { MinimumAngle = 28.0 };
            var mesh = (TriangleNet.Mesh)polygon.Triangulate(copt, qopt);

            // 4) Post-filter: çox uzun kənarlı üçbucaqları at + fallback
            float cap = ComputeEdgeCapFromNearestNeighbors(Vertices, 3.0f, 20f);
            int created = TransferTrianglesWithEdgeCap_ByRef(mesh, tnByVm, cap);

            if (created == 0)
            {
                // Çox sərt olmuşuq – limiti yumşalt və yenə köçür
                Triangles.Clear();
                created = TransferTrianglesWithEdgeCap_ByRef(mesh, tnByVm, cap * 2.0f);
            }

            SaveRigCommand.NotifyCanExecuteChanged();
            RequestRedraw?.Invoke(this, EventArgs.Empty);

            MessageBox.Show($"Triangle.NET: {mesh.Triangles.Count} | Əlavə olunan: {created}", "Avto Üçbucaq");
        }

        private bool CanAutoTriangle() => Vertices.Count >= 3;

        // =============== HELPERS ===============

        // Monotone chain (O(n log n)) – VertexModel üzrə konveks hull qaytarır
        private static List<VertexModel> ComputeConvexHull(IList<VertexModel> verts)
        {
            var pts = verts
                .Select(v => (vm: v, x: v.BindPosition.X, y: v.BindPosition.Y))
                .OrderBy(t => t.x).ThenBy(t => t.y)
                .ToList();

            float Cross((VertexModel vm, float x, float y) o, (VertexModel vm, float x, float y) a, (VertexModel vm, float x, float y) b)
                => (a.x - o.x) * (b.y - o.y) - (a.y - o.y) * (b.x - o.x);

            var lower = new List<(VertexModel vm, float x, float y)>();
            foreach (var p in pts)
            {
                while (lower.Count >= 2 && Cross(lower[^2], lower[^1], p) <= 0) lower.RemoveAt(lower.Count - 1);
                lower.Add(p);
            }

            var upper = new List<(VertexModel vm, float x, float y)>();
            for (int i = pts.Count - 1; i >= 0; i--)
            {
                var p = pts[i];
                while (upper.Count >= 2 && Cross(upper[^2], upper[^1], p) <= 0) upper.RemoveAt(upper.Count - 1);
                upper.Add(p);
            }

            // son elementlər təkrardır, at
            lower.RemoveAt(lower.Count - 1);
            upper.RemoveAt(upper.Count - 1);

            return lower.Concat(upper).Select(t => t.vm).ToList();
        }

        // TN vertex → VM map ilə köçürür, uzun kənarlı üçbucaqları atır
        private int TransferTrianglesWithEdgeCap_ByRef(
            TriangleNet.Mesh mesh,
            Dictionary<VertexModel, TriangleNet.Geometry.Vertex> vmToTn, // tərs map lazımdır
            float cap)
        {
            // tərs xəritə: TN → VM
            var tnToVm = vmToTn.ToDictionary(kv => kv.Value, kv => kv.Key);

            int created = 0;
            foreach (var t in mesh.Triangles)
            {
                var v0 = t.GetVertex(0);
                var v1 = t.GetVertex(1);
                var v2 = t.GetVertex(2);
                if (v0 == null || v1 == null || v2 == null) continue;

                if (!tnToVm.TryGetValue(v0, out var a) ||
                    !tnToVm.TryGetValue(v1, out var b) ||
                    !tnToVm.TryGetValue(v2, out var c)) continue;

                if (HasEdgeLongerThan(a, b, c, cap)) continue;

                if (!TriangleExists(a, b, c))
                {
                    Triangles.Add(new TriangleModel(a, b, c));
                    created++;
                }
            }
            return created;
        }

        private static float ComputeEdgeCapFromNearestNeighbors(
            IList<VertexModel> verts, float multiplier = 3.0f, float minCap = 20f)
        {
            if (verts == null || verts.Count < 2) return float.MaxValue;

            var nn = new List<float>(verts.Count);
            for (int i = 0; i < verts.Count; i++)
            {
                var vi = verts[i].BindPosition;
                float best = float.MaxValue;
                for (int j = 0; j < verts.Count; j++)
                {
                    if (i == j) continue;
                    var vj = verts[j].BindPosition;
                    float dx = vi.X - vj.X, dy = vi.Y - vj.Y;
                    float d = MathF.Sqrt(dx * dx + dy * dy);
                    if (d < best) best = d;
                }
                if (best < float.MaxValue) nn.Add(best);
            }
            nn.Sort();
            float median = nn.Count > 0 ? nn[nn.Count / 2] : 0f;
            return MathF.Max(median * multiplier, minCap);
        }

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






        // === PARAMETRLƏR (istəsən slider-lərə bağlayarsan) ===
        private const float AutoRadiusPx = 12f;  // baza qalınlıq (px)
        private const float JointCircleRadiusPx = 9f;   // oynaq dairəsi radiusu
        private const int JointCirclePoints = 8;    // dairə üçün nöqtə sayı
        private const float RingsPer100px = 4f;   // 100px sümük uzunluğuna neçə “ring”
        private const float DedupTolerancePx = 3f;   // yaxın nöqtələri birləşdir

        [RelayCommand(CanExecute = nameof(CanAutoGenVertices))]
        private void AutoGenerateVertices()
        {
            if (!CanAutoGenVertices()) return;

            // İstəsən mövcud nöqtələri saxla; indi təmiz başlayırıq
            Vertices.Clear();
            Triangles.Clear();
            _vertexIdCounter = 0;

            var temp = new List<SKPoint>(1024);

            // 1) Sümük boyunca ikili zolaq
            foreach (var j in Joints)
            {
                if (j.Parent == null) continue;

                var a = j.Parent.Position;
                var b = j.Position;
                var dir = new SKPoint(b.X - a.X, b.Y - a.Y);
                float len = MathF.Sqrt(dir.X * dir.X + dir.Y * dir.Y);
                if (len < 1e-3f) continue;

                dir = new SKPoint(dir.X / len, dir.Y / len);
                var nrm = new SKPoint(-dir.Y, dir.X);

                // sümük uzunluğuna görə ring sayı
                int rings = Math.Max(1, (int)MathF.Round((len / 100f) * RingsPer100px));

                for (int i = 0; i <= rings; i++)
                {
                    float t = rings == 0 ? 0f : (float)i / rings;
                    var p = new SKPoint(a.X + dir.X * (t * len),
                                        a.Y + dir.Y * (t * len));

                    // uclar daha incə olsun (0.8 .. 1.0 .. 0.8)
                    float edgeTaper = 0.8f + 0.2f * (1f - MathF.Abs(0.5f - t) * 2f);
                    float r = AutoRadiusPx * edgeTaper;

                    temp.Add(new SKPoint(p.X + nrm.X * r, p.Y + nrm.Y * r));
                    temp.Add(new SKPoint(p.X - nrm.X * r, p.Y - nrm.Y * r));
                }
            }

            // 2) Oynaq ətrafında dairəvi dəstək nöqtələri
            foreach (var j in Joints)
            {
                AddCirclePoints(temp, j.Position, JointCircleRadiusPx, JointCirclePoints);
            }

            // 3) Yaxın nöqtələri dedup et
            var unique = DedupPoints(temp, DedupTolerancePx);

            // 4) VertexModel-lərə çevir
            foreach (var p in unique)
            {
                var v = new VertexModel(_vertexIdCounter++, p);
                Vertices.Add(v);
            }

            // 5) Avto-triangulation-u çağır
            AutoTriangle();

            SaveRigCommand.NotifyCanExecuteChanged();
            AutoWeightCommand.NotifyCanExecuteChanged();
            RequestRedraw?.Invoke(this, EventArgs.Empty);

            MessageBox.Show($"AutoMesh: {unique.Count} nöqtə yaradıldı, {Triangles.Count} üçbucaq quruldu.", "Uğurlu");
        }

        private bool CanAutoGenVertices()
        {
            return IsImageLoaded && Joints.Count >= 2;
        }

        private static void AddCirclePoints(List<SKPoint> acc, SKPoint c, float r, int count)
        {
            for (int i = 0; i < count; i++)
            {
                float ang = (float)(i * (2 * Math.PI / count));
                acc.Add(new SKPoint(c.X + r * MathF.Cos(ang),
                                    c.Y + r * MathF.Sin(ang)));
            }
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


    }
}