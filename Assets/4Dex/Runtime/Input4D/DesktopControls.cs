using System;
using FourDex.Math4D;
using FourDex.Player;
using FourDex.XR;
using UnityEngine;

namespace FourDex.Runtime
{
    /// <summary>One frame of desktop input, already read from devices. Rates are -1..1 unless noted.</summary>
    public struct DesktopCommands
    {
        /// <summary>Head look in degrees this frame (yaw right, pitch down). Only used with a simulated head.</summary>
        public Vector2 LookDegrees;

        /// <summary>Simulated head only: roll rate (-1..1) and lean rate (-1..1, + forward), to exercise head mappings.</summary>
        public float HeadRoll, HeadLean;

        /// <summary>Walk: x = strafe right, y = forward. Head-relative, in the player's own 3-space.</summary>
        public Vector2 Walk;

        /// <summary>§7A: + is ana, - is kata, along world W.</summary>
        public float AnaKata;

        /// <summary>§7B, in the player's own planes. Each + turns the plane's first axis toward its second.</summary>
        public float XW, YW, ZW, XZ;

        /// <summary>§7C: rate, plus a one-off step in W units (mouse wheel).</summary>
        public float Focus;
        public float FocusStep;

        public bool Fast;

        /// <summary>SPEC §22 emergency reset: conventional orientation, focus on own slice, head recentred. Keeps position.</summary>
        public bool ResetView;

        /// <summary>Back to the scene's authored start, position included.</summary>
        public bool ResetAll;
    }

    [Serializable]
    public struct DesktopSpeeds
    {
        [Tooltip("Walking, metres per second.")] public float Walk;
        [Tooltip("Ana/kata, W units per second.")] public float AnaKata;
        [Tooltip("Rotation through the 4D planes, degrees per second.")] public float Rotate;
        [Tooltip("focusW change, W units per second.")] public float Focus;
        [Tooltip("Multiplier while Shift is held.")] public float FastMultiplier;
        [Tooltip("Simulated head roll, degrees per second.")] public float HeadRoll;
        [Tooltip("Simulated head lean, metres per second.")] public float HeadLean;

        public static DesktopSpeeds Default => new DesktopSpeeds
        {
            Walk = 1.5f,
            AnaKata = 0.75f,
            Rotate = 45f,
            Focus = 0.5f,
            FastMultiplier = 3f,
            HeadRoll = 40f,
            HeadLean = 0.25f,
        };
    }

    /// <summary>
    /// Applies desktop commands to the player state and simulated head. Device-free, so the rules are tested
    /// directly. Each command routes to exactly one of SPEC §7's operations: move, rotate or focus.
    /// </summary>
    public static class DesktopControls
    {
        /// <param name="head">The simulated head, or null when a real HMD supplies the pose.</param>
        /// <param name="headYaw">Current head yaw, used to make walking head-relative.</param>
        /// <param name="resetAll">Called for <see cref="DesktopCommands.ResetAll"/>; the caller owns the authored start.</param>
        public static void Apply(Player4DState player, SimulatedHmdPoseSource? head, Quaternion headYaw,
            in DesktopCommands c, in DesktopSpeeds speeds, float dt, Action? resetAll = null)
        {
            if (c.ResetAll)
            {
                resetAll?.Invoke();
                head?.Recenter();
                return;
            }
            if (c.ResetView)
            {
                player.ResetOrientation();
                player.SetFocusW(0f);
                head?.Recenter();
                return;
            }

            float scale = c.Fast ? speeds.FastMultiplier : 1f;

            head?.Look(c.LookDegrees.x, c.LookDegrees.y);
            head?.Tilt(c.HeadRoll * speeds.HeadRoll * dt);
            head?.LeanBy(c.HeadLean * speeds.HeadLean * dt);

            // §7A move. Tracking space is the player's own frame, so a head-relative direction is a local delta.
            if (c.Walk != Vector2.zero)
            {
                Vector3 local = headYaw * new Vector3(c.Walk.x, 0f, c.Walk.y);
                player.MoveLocal(new Vec4(local.x, local.y, local.z, 0f) * (speeds.Walk * scale * dt));
            }
            if (c.AnaKata != 0f)
                player.MoveAnaKata(c.AnaKata * speeds.AnaKata * scale * dt);

            // §7B rotate.
            float step = speeds.Rotate * Mathf.Deg2Rad * scale * dt;
            if (c.XW != 0f) player.RotateLocal(Plane4.XW, c.XW * step);
            if (c.YW != 0f) player.RotateLocal(Plane4.YW, c.YW * step);
            if (c.ZW != 0f) player.RotateLocal(Plane4.ZW, c.ZW * step);
            if (c.XZ != 0f) player.RotateLocal(Plane4.XZ, c.XZ * step);

            // §7C focus.
            float focusDelta = c.Focus * speeds.Focus * scale * dt + c.FocusStep;
            if (focusDelta != 0f)
                player.AdjustFocusW(focusDelta);
        }
    }
}
