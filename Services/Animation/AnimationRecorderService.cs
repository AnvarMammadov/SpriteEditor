using System;
using System.Collections.ObjectModel;
using System.Linq;
using SkiaSharp;
using SpriteEditor.Data;
using SpriteEditor.ViewModels;

namespace SpriteEditor.Services.Animation
{
    /// <summary>
    /// Service for recording and playing back skeletal animations.
    /// Captures poses as keyframes and interpolates between them.
    /// </summary>
    public class AnimationRecorderService
    {
        // Current playback state
        private AnimationClip _currentClip;
        private float _playbackTime = 0f;
        private bool _isPlaying = false;

        /// <summary>
        /// Current playback time in seconds.
        /// </summary>
        public float PlaybackTime => _playbackTime;

        /// <summary>
        /// Whether animation is currently playing.
        /// </summary>
        public bool IsPlaying => _isPlaying;

        /// <summary>
        /// Event fired when playback time changes (for UI updates).
        /// </summary>
        public event EventHandler<float> PlaybackTimeChanged;

        /// <summary>
        /// Event fired when playback starts or stops.
        /// </summary>
        public event EventHandler<bool> PlaybackStateChanged;

        /// <summary>
        /// Creates a new animation clip.
        /// </summary>
        public AnimationClip CreateClip(string name, float duration, int fps = 30)
        {
            return new AnimationClip(name, duration, fps);
        }

        /// <summary>
        /// Records the current skeleton pose as a keyframe.
        /// </summary>
        public Keyframe RecordKeyframe(ObservableCollection<JointModel> joints, float time)
        {
            if (joints == null || joints.Count == 0)
                throw new ArgumentException("Joints collection cannot be null or empty", nameof(joints));

            var keyframe = new Keyframe(time);

            foreach (var joint in joints)
            {
                keyframe.JointPoses[joint.Id] = new JointPose(
                    joint.Position,
                    joint.Rotation
                );
            }

            return keyframe;
        }

        /// <summary>
        /// Applies a pose from a keyframe to the skeleton.
        /// </summary>
        public void ApplyPose(ObservableCollection<JointModel> joints, Keyframe keyframe)
        {
            if (joints == null || keyframe == null)
                return;

            foreach (var joint in joints)
            {
                if (keyframe.JointPoses.TryGetValue(joint.Id, out var pose))
                {
                    joint.Position = pose.Position;
                    joint.Rotation = pose.Rotation;

                    // Update parent relationship data
                    if (joint.Parent != null)
                    {
                        float dx = joint.Position.X - joint.Parent.Position.X;
                        float dy = joint.Position.Y - joint.Parent.Position.Y;
                        joint.BoneLength = MathF.Sqrt(dx * dx + dy * dy);
                    }
                }
            }
        }

        /// <summary>
        /// Applies an interpolated pose at a specific time in the animation.
        /// </summary>
        public void ApplyPoseAtTime(ObservableCollection<JointModel> joints, AnimationClip clip, float time)
        {
            if (joints == null || clip == null || clip.Keyframes.Count == 0)
                return;

            // Clamp time to animation duration
            time = Math.Clamp(time, 0, clip.Duration);

            // Get surrounding keyframes
            var (before, after) = clip.GetSurroundingKeyframes(time);

            if (before == null || after == null)
                return;

            // If we're exactly on a keyframe, just apply it
            if (before == after || MathF.Abs(before.Time - after.Time) < 0.001f)
            {
                ApplyPose(joints, before);
                return;
            }

            // Interpolate between keyframes
            float t = (time - before.Time) / (after.Time - before.Time);

            // Apply easing function
            t = ApplyEasing(t, clip.Easing);

            // Apply interpolated pose
            var interpolatedKeyframe = InterpolateKeyframes(before, after, t);
            ApplyPose(joints, interpolatedKeyframe);
        }

        /// <summary>
        /// Interpolates between two keyframes.
        /// </summary>
        private Keyframe InterpolateKeyframes(Keyframe a, Keyframe b, float t)
        {
            var result = new Keyframe(a.Time + (b.Time - a.Time) * t);

            // Get all unique joint IDs from both keyframes
            var allJointIds = a.JointPoses.Keys.Union(b.JointPoses.Keys).ToList();

            foreach (var jointId in allJointIds)
            {
                JointPose poseA = a.JointPoses.ContainsKey(jointId) 
                    ? a.JointPoses[jointId] 
                    : (b.JointPoses.ContainsKey(jointId) ? b.JointPoses[jointId] : default);

                JointPose poseB = b.JointPoses.ContainsKey(jointId) 
                    ? b.JointPoses[jointId] 
                    : poseA;

                result.JointPoses[jointId] = JointPose.Lerp(poseA, poseB, t);
            }

            return result;
        }

        /// <summary>
        /// Applies easing function to interpolation parameter t.
        /// </summary>
        private float ApplyEasing(float t, EasingFunction easing)
        {
            return easing switch
            {
                EasingFunction.Linear => t,
                EasingFunction.EaseIn => t * t,
                EasingFunction.EaseOut => 1 - (1 - t) * (1 - t),
                EasingFunction.EaseInOut => t < 0.5f 
                    ? 2 * t * t 
                    : 1 - MathF.Pow(-2 * t + 2, 2) / 2,
                EasingFunction.Bounce => BounceEaseOut(t),
                EasingFunction.Elastic => ElasticEaseOut(t),
                _ => t
            };
        }

        private float BounceEaseOut(float t)
        {
            const float n1 = 7.5625f;
            const float d1 = 2.75f;

            if (t < 1 / d1)
                return n1 * t * t;
            else if (t < 2 / d1)
                return n1 * (t -= 1.5f / d1) * t + 0.75f;
            else if (t < 2.5 / d1)
                return n1 * (t -= 2.25f / d1) * t + 0.9375f;
            else
                return n1 * (t -= 2.625f / d1) * t + 0.984375f;
        }

        private float ElasticEaseOut(float t)
        {
            const float c4 = (2 * MathF.PI) / 3;
            return t == 0 ? 0 : t == 1 ? 1 : MathF.Pow(2, -10 * t) * MathF.Sin((t * 10 - 0.75f) * c4) + 1;
        }

        /// <summary>
        /// Starts playing the animation.
        /// </summary>
        public void Play(AnimationClip clip, float startTime = 0f)
        {
            if (clip == null)
                throw new ArgumentNullException(nameof(clip));

            if (!clip.IsValid(out string error))
                throw new ArgumentException($"Invalid animation clip: {error}");

            _currentClip = clip;
            _playbackTime = startTime;
            _isPlaying = true;

            PlaybackStateChanged?.Invoke(this, true);
        }

        /// <summary>
        /// Pauses playback.
        /// </summary>
        public void Pause()
        {
            _isPlaying = false;
            PlaybackStateChanged?.Invoke(this, false);
        }

        /// <summary>
        /// Stops playback and resets to start.
        /// </summary>
        public void Stop()
        {
            _isPlaying = false;
            _playbackTime = 0f;
            PlaybackStateChanged?.Invoke(this, false);
            PlaybackTimeChanged?.Invoke(this, 0f);
        }

        /// <summary>
        /// Updates playback time (call this in your render loop).
        /// Returns true if animation is still playing, false if finished.
        /// </summary>
        public bool Update(float deltaTime, ObservableCollection<JointModel> joints)
        {
            if (!_isPlaying || _currentClip == null)
                return false;

            _playbackTime += deltaTime;

            // Check if we've reached the end
            if (_playbackTime >= _currentClip.Duration)
            {
                if (_currentClip.IsLooping)
                {
                    _playbackTime = _playbackTime % _currentClip.Duration;
                }
                else
                {
                    _playbackTime = _currentClip.Duration;
                    _isPlaying = false;
                    PlaybackStateChanged?.Invoke(this, false);
                }
            }

            // Apply pose at current time
            if (joints != null)
            {
                ApplyPoseAtTime(joints, _currentClip, _playbackTime);
            }

            PlaybackTimeChanged?.Invoke(this, _playbackTime);

            return _isPlaying;
        }

        /// <summary>
        /// Seeks to a specific time in the animation.
        /// </summary>
        public void Seek(float time)
        {
            if (_currentClip == null)
                return;

            _playbackTime = Math.Clamp(time, 0, _currentClip.Duration);
            PlaybackTimeChanged?.Invoke(this, _playbackTime);
        }

        /// <summary>
        /// Gets the current clip being played.
        /// </summary>
        public AnimationClip GetCurrentClip()
        {
            return _currentClip;
        }
    }
}
