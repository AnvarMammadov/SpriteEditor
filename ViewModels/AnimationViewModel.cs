using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SpriteEditor.Data;
using SpriteEditor.Services.Animation;
using SpriteEditor.Views;

namespace SpriteEditor.ViewModels
{
    /// <summary>
    /// ViewModel for animation recording and playback.
    /// Manages keyframes, timeline, and playback controls.
    /// </summary>
    public partial class AnimationViewModel : ObservableObject
    {
        private readonly AnimationRecorderService _recorderService;
        private readonly Func<ObservableCollection<JointModel>> _getJoints; // Delegate to get current skeleton
        private DispatcherTimer _playbackTimer;

        public event EventHandler RequestRedraw;

        // === ANIMATION CLIPS ===

        [ObservableProperty]
        private ObservableCollection<AnimationClip> _animationClips = new ObservableCollection<AnimationClip>();

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(RecordKeyframeCommand))]
        [NotifyCanExecuteChangedFor(nameof(PlayCommand))]
        [NotifyCanExecuteChangedFor(nameof(DeleteKeyframeCommand))]
        private AnimationClip _currentClip;

        // === PLAYBACK STATE ===

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(PauseCommand))]
        [NotifyCanExecuteChangedFor(nameof(StopCommand))]
        private bool _isPlaying = false;

        [ObservableProperty]
        private float _currentTime = 0f;

        [ObservableProperty]
        private Keyframe _selectedKeyframe;

        // === TIMELINE UI ===

        [ObservableProperty]
        private float _timelineZoom = 1.0f; // Pixels per second

        [ObservableProperty]
        private float _timelineScroll = 0f;

        // === RECORDING MODE ===

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(RecordKeyframeCommand))]
        private bool _isRecordingMode = false;

        public AnimationViewModel(AnimationRecorderService recorderService, Func<ObservableCollection<JointModel>> getJoints)
        {
            _recorderService = recorderService ?? throw new ArgumentNullException(nameof(recorderService));
            _getJoints = getJoints ?? throw new ArgumentNullException(nameof(getJoints));

            // Setup playback timer (60 FPS)
            _playbackTimer = new DispatcherTimer();
            _playbackTimer.Interval = TimeSpan.FromMilliseconds(16);
            _playbackTimer.Tick += PlaybackTimer_Tick;

            // Subscribe to recorder events
            _recorderService.PlaybackTimeChanged += (s, time) =>
            {
                CurrentTime = time;
            };

            _recorderService.PlaybackStateChanged += (s, playing) =>
            {
                IsPlaying = playing;
            };

            // Create default animation clip
            CreateDefaultClip();
        }

        private void CreateDefaultClip()
        {
            var defaultClip = new AnimationClip("New Animation", 2.0f, 30);
            AnimationClips.Add(defaultClip);
            CurrentClip = defaultClip;
        }

        // ========================================
        // === COMMANDS ===
        // ========================================

        [RelayCommand(CanExecute = nameof(CanRecordKeyframe))]
        private void RecordKeyframe()
        {
            if (CurrentClip == null)
            {
                CustomMessageBox.Show("Please create an animation clip first.", "No Clip", System.Windows.MessageBoxButton.OK, MsgImage.Warning);
                return;
            }

            var joints = _getJoints();
            if (joints == null || joints.Count == 0)
            {
                CustomMessageBox.Show("No skeleton found. Please bind a template first.", "No Skeleton", System.Windows.MessageBoxButton.OK, MsgImage.Warning);
                return;
            }

            // Record keyframe at current time
            var keyframe = _recorderService.RecordKeyframe(joints, CurrentTime);
            CurrentClip.AddKeyframe(keyframe);

            CustomMessageBox.Show(
                $"Keyframe recorded at {CurrentTime:F2}s\n\nTotal keyframes: {CurrentClip.Keyframes.Count}",
                "Keyframe Recorded",
                System.Windows.MessageBoxButton.OK,
                MsgImage.Success
            );

            RequestRedraw?.Invoke(this, EventArgs.Empty);
        }

        private bool CanRecordKeyframe() => CurrentClip != null && IsRecordingMode;

        [RelayCommand(CanExecute = nameof(CanPlay))]
        private void Play()
        {
            if (CurrentClip == null || CurrentClip.Keyframes.Count < 2)
            {
                CustomMessageBox.Show(
                    "Please add at least 2 keyframes to play animation.",
                    "Not Enough Keyframes",
                    System.Windows.MessageBoxButton.OK,
                    MsgImage.Warning
                );
                return;
            }

            try
            {
                _recorderService.Play(CurrentClip, CurrentTime);
                _playbackTimer.Start();
            }
            catch (Exception ex)
            {
                CustomMessageBox.Show($"Failed to play animation:\n{ex.Message}", "Playback Error", System.Windows.MessageBoxButton.OK, MsgImage.Error);
            }
        }

        private bool CanPlay() => CurrentClip != null && !IsPlaying && CurrentClip.Keyframes.Count >= 2;

        [RelayCommand(CanExecute = nameof(CanPause))]
        private void Pause()
        {
            _recorderService.Pause();
            _playbackTimer.Stop();
        }

        private bool CanPause() => IsPlaying;

        [RelayCommand(CanExecute = nameof(CanStop))]
        private void Stop()
        {
            _recorderService.Stop();
            _playbackTimer.Stop();
            RequestRedraw?.Invoke(this, EventArgs.Empty);
        }

        private bool CanStop() => IsPlaying || CurrentTime > 0;

        [RelayCommand]
        private void Seek(float time)
        {
            if (CurrentClip == null) return;

            CurrentTime = Math.Clamp(time, 0, CurrentClip.Duration);
            _recorderService.Seek(CurrentTime);

            // Apply pose at seek time
            var joints = _getJoints();
            if (joints != null)
            {
                _recorderService.ApplyPoseAtTime(joints, CurrentClip, CurrentTime);
                RequestRedraw?.Invoke(this, EventArgs.Empty);
            }
        }

        [RelayCommand]
        private void CreateNewClip(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                name = $"Animation {AnimationClips.Count + 1}";

            var newClip = new AnimationClip(name, 2.0f, 30);
            AnimationClips.Add(newClip);
            CurrentClip = newClip;

            CustomMessageBox.Show($"Created new animation clip: {name}", "Success", System.Windows.MessageBoxButton.OK, MsgImage.Success);
        }

        [RelayCommand(CanExecute = nameof(CanDeleteKeyframe))]
        private void DeleteKeyframe(Keyframe keyframe)
        {
            if (keyframe == null || CurrentClip == null) return;

            CurrentClip.RemoveKeyframeAt(keyframe.Time);
            SelectedKeyframe = null;

            RequestRedraw?.Invoke(this, EventArgs.Empty);
        }

        private bool CanDeleteKeyframe() => SelectedKeyframe != null && CurrentClip != null;

        [RelayCommand]
        private void DeleteCurrentClip()
        {
            if (CurrentClip == null) return;

            var result = System.Windows.MessageBox.Show(
                $"Are you sure you want to delete animation '{CurrentClip.Name}'?",
                "Confirm Delete",
                System.Windows.MessageBoxButton.YesNo,
                System.Windows.MessageBoxImage.Warning
            );

            if (result == System.Windows.MessageBoxResult.Yes)
            {
                AnimationClips.Remove(CurrentClip);
                CurrentClip = AnimationClips.FirstOrDefault();
            }
        }

        // ========================================
        // === PLAYBACK TIMER ===
        // ========================================

        private void PlaybackTimer_Tick(object sender, EventArgs e)
        {
            var joints = _getJoints();
            if (joints == null) return;

            bool stillPlaying = _recorderService.Update(0.016f, joints);

            if (!stillPlaying)
            {
                _playbackTimer.Stop();
            }

            RequestRedraw?.Invoke(this, EventArgs.Empty);
        }

        // ========================================
        // === TIMELINE HELPERS ===
        // ========================================

        /// <summary>
        /// Converts time (seconds) to pixel position on timeline.
        /// </summary>
        public float TimeToPixel(float time)
        {
            return (time * 100 * TimelineZoom) - TimelineScroll;
        }

        /// <summary>
        /// Converts pixel position on timeline to time (seconds).
        /// </summary>
        public float PixelToTime(float pixel)
        {
            return (pixel + TimelineScroll) / (100 * TimelineZoom);
        }

        /// <summary>
        /// Gets nearest keyframe to the specified time.
        /// </summary>
        public Keyframe GetNearestKeyframe(float time, float threshold = 0.1f)
        {
            if (CurrentClip == null) return null;

            return CurrentClip.Keyframes
                .Where(kf => MathF.Abs(kf.Time - time) < threshold)
                .OrderBy(kf => MathF.Abs(kf.Time - time))
                .FirstOrDefault();
        }

        /// <summary>
        /// Cleanup on disposal.
        /// </summary>
        public void Dispose()
        {
            _playbackTimer?.Stop();
            _playbackTimer = null;
        }
    }
}
