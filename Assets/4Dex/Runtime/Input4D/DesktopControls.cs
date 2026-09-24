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

        /// <summary>
        /// Simulated head look from the keyboard: x turns right, y looks up, each -1..1. The mouse does the same job
        /// through <see cref="LookDegrees"/>; this exists because mouse input does not always reach the Game view.
        /// </summary>
        public Vector2 LookKeys;

        /// <summary>Walk: x = strafe right, y = forward. Head-relative, in the player's own 3-space.</summary>
        public Vector2 Walk;

        /// <summary>§7A: + is ana, - is kata, along world W.</summary>
        public float AnaKata;

        /// <summary>
        /// §7A: + is up, - is down, along world Y. There is no climbing in the 4D model, so this is a testing aid for
        /// worlds with more than one level, not a game mechanic.
        /// </summary>
        public float Fly;

        /// <summary>§7B, in the player's own planes. Each + turns the plane's first axis toward its second.</summary>
        public float XW, YW, ZW, XZ;

        /// <summary>Ordinary turn pressed this frame, -1, 0 or +1: what snap turning steps on (SPEC §22).</summary>
        public int XZPress;

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
        [Tooltip("Vertical flight, metres per second.")] public float Fly;
        [Tooltip("Rotation through the 4D planes, degrees per second.")] public float Rotate;
        [Tooltip("focusW change, W units per second.")] public float Focus;
        [Tooltip("Multiplier while Shift is held.")] public float FastMultiplier;
        [Tooltip("Simulated head look from the keyboard, degrees per second.")] public float HeadLook;
        [Tooltip("Simulated head roll, degrees per second.")] public float HeadRoll;
        [Tooltip("Simulated head lean, metres per second.")] public float HeadLean;

        public static DesktopSpeeds Default => new DesktopSpeeds
        {
            Walk = 1.5f,
            AnaKata = 0.75f,
            Fly = 1.5f,
            Rotate = 45f,
            Focus = 0.5f,
            FastMultiplier = 3f,
            HeadLook = 75f,
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
        // Smooth turning, the conventional default, when no comfort settings are supplied.
        private static readonly ComfortParameters DefaultComfort = new ComfortParameters();

        /// <param name="head">The simulated head, or null when a real HMD supplies the pose.</param>
        /// <param name="headYaw">Current head yaw, used to make walking head-relative.</param>
        /// <param name="resetAll">Called for <see cref="DesktopCommands.ResetAll"/>; the caller owns the authored start.</param>
        /// <param name="comfort">SPEC §22 comfort settings; without them ordinary turning is smooth.</param>
        public static void Apply(Player4DState player, SimulatedHmdPoseSource? head, Quaternion headYaw,
            in DesktopCommands c, in DesktopSpeeds speeds, float dt, Action? resetAll = null, ComfortParameters? comfort = null)
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

            // Mouse look and keyboard look add up: whichever of the two is reaching the game does the work.
            // Look() takes Unity pitch, where positive looks down, so looking up is a negative step.
            head?.Look(c.LookDegrees.x + c.LookKeys.x * speeds.HeadLook * dt,
                c.LookDegrees.y - c.LookKeys.y * speeds.HeadLook * dt);
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
            if (c.Fly != 0f)
                player.MoveWorld(new Vec4(0f, c.Fly * speeds.Fly * scale * dt, 0f, 0f));

            // §7B rotate.
            float step = speeds.Rotate * Mathf.Deg2Rad * scale * dt;
            if (c.XW != 0f) player.RotateLocal(Plane4.XW, c.XW * step);
            if (c.YW != 0f) player.RotateLocal(Plane4.YW, c.YW * step);
            if (c.ZW != 0f) player.RotateLocal(Plane4.ZW, c.ZW * step);
            // Ordinary turning can be snapped; the XW/YW/ZW rotations above are the experiment itself and stay smooth.
            float turn = ComfortTurn.Step(c.XZ, c.XZPress, speeds.Rotate * scale, comfort ?? DefaultComfort, dt);
            if (turn != 0f) player.RotateLocal(Plane4.XZ, turn);

            // §7C focus.
            float focusDelta = c.Focus * speeds.Focus * scale * dt + c.FocusStep;
            if (focusDelta != 0f)
                player.AdjustFocusW(focusDelta);
        }
    }
}
