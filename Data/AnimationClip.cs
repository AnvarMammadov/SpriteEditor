using System;
using System.Collections.Generic;
using System.Linq;

namespace SpriteEditor.Data
{
    /// <summary>
    /// Represents a complete animation clip with multiple keyframes.
    /// </summary>
    public class AnimationClip
    {
        /// <summary>
        /// Unique name for this animation (e.g., "Idle", "Walk", "Jump").
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Duration of the animation in seconds.
        /// </summary>
        public float Duration { get; set; }

        /// <summary>
        /// Frames per second for playback and export.
        /// </summary>
        public int FPS { get; set; } = 30;

        /// <summary>
        /// List of keyframes, ordered by time.
        /// </summary>
        public List<Keyframe> Keyframes { get; set; } = new List<Keyframe>();

        /// <summary>
        /// Easing function applied to interpolation between keyframes.
        /// </summary>
        public EasingFunction Easing { get; set; } = EasingFunction.Linear;

        /// <summary>
        /// Whether this animation should loop.
        /// </summary>
        public bool IsLooping { get; set; } = false;

        public AnimationClip()
        {
        }

        public AnimationClip(string name, float duration, int fps = 30)
        {
            Name = name;
            Duration = duration;
            FPS = fps;
        }

        /// <summary>
        /// Adds a keyframe at the specified time, maintaining sorted order.
        /// If a keyframe already exists at this time, it will be replaced.
        /// </summary>
        public void AddKeyframe(Keyframe keyframe)
        {
            // Remove existing keyframe at same time
            RemoveKeyframeAt(keyframe.Time);

            // Insert in sorted order
            int insertIndex = Keyframes.Count;
            for (int i = 0; i < Keyframes.Count; i++)
            {
                if (Keyframes[i].Time > keyframe.Time)
                {
                    insertIndex = i;
                    break;
                }
            }

            Keyframes.Insert(insertIndex, keyframe);
        }

        /// <summary>
        /// Removes keyframe at the specified time (within 0.01s tolerance).
        /// </summary>
        public bool RemoveKeyframeAt(float time, float tolerance = 0.01f)
        {
            for (int i = 0; i < Keyframes.Count; i++)
            {
                if (MathF.Abs(Keyframes[i].Time - time) < tolerance)
                {
                    Keyframes.RemoveAt(i);
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Gets the keyframe at or closest to the specified time.
        /// </summary>
        public Keyframe GetKeyframeAt(float time, float tolerance = 0.01f)
        {
            return Keyframes.FirstOrDefault(kf => MathF.Abs(kf.Time - time) < tolerance);
        }

        /// <summary>
        /// Gets the two keyframes that surround the specified time.
        /// Returns (null, null) if time is outside the animation range.
        /// Returns (keyframe, keyframe) if time exactly matches a keyframe.
        /// </summary>
        public (Keyframe before, Keyframe after) GetSurroundingKeyframes(float time)
        {
            if (Keyframes.Count == 0)
                return (null, null);

            // Before first keyframe
            if (time <= Keyframes[0].Time)
                return (Keyframes[0], Keyframes[0]);

            // After last keyframe
            if (time >= Keyframes[^1].Time)
                return (Keyframes[^1], Keyframes[^1]);

            // Find surrounding keyframes
            for (int i = 0; i < Keyframes.Count - 1; i++)
            {
                if (time >= Keyframes[i].Time && time <= Keyframes[i + 1].Time)
                {
                    return (Keyframes[i], Keyframes[i + 1]);
                }
            }

            return (null, null);
        }

        /// <summary>
        /// Validates the animation clip.
        /// </summary>
        public bool IsValid(out string errorMessage)
        {
            if (string.IsNullOrWhiteSpace(Name))
            {
                errorMessage = "Animation name cannot be empty";
                return false;
            }

            if (Duration <= 0)
            {
                errorMessage = "Duration must be positive";
                return false;
            }

            if (FPS <= 0)
            {
                errorMessage = "FPS must be positive";
                return false;
            }

            if (Keyframes.Count < 1)
            {
                errorMessage = "Animation must have at least 1 keyframe";
                return false;
            }

            // Check for keyframes outside duration
            if (Keyframes.Any(kf => kf.Time < 0 || kf.Time > Duration))
            {
                errorMessage = "All keyframes must be within animation duration";
                return false;
            }

            errorMessage = null;
            return true;
        }
    }

    /// <summary>
    /// Easing functions for interpolation between keyframes.
    /// </summary>
    public enum EasingFunction
    {
        Linear,
        EaseIn,
        EaseOut,
        EaseInOut,
        Bounce,
        Elastic
    }
}
