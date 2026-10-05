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

        /// <summary>§7A: + is ana, - is kata, along the W that <see cref="NavigationParameters.AnaKataFrame"/> picks.</summary>
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

        /// <summary>W turns pressed this frame, -1, 0 or +1: what stepped XW/YW/ZW turns step on.</summary>
        public int XWPress, YWPress, ZWPress;

        /// <summary>Turn to the nearest axis-aligned orientation.</summary>
        public bool Align;

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
        /// <param name="assist">Plays out stepped turns, aligns and settles. Without it W turning is smooth.</param>
        /// <param name="navigation">Navigation aids; without them, <see cref="NavigationParameters.Unassisted"/>.</param>
        /// <param name="cellSize">The maze's cell size for settling, or 0 when the scene has no maze.</param>
        public static void Apply(Player4DState player, SimulatedHmdPoseSource? head, Quaternion headYaw,
            in DesktopCommands c, in DesktopSpeeds speeds, float dt, Action? resetAll = null, ComfortParameters? comfort = null,
            NavigationAssist? assist = null, NavigationParameters? navigation = null, float cellSize = 0f)
        {
            NavigationParameters nav = navigation ?? NavigationParameters.Unassisted;
            if (c.ResetAll)
            {
                assist?.Cancel();
                resetAll?.Invoke();
                head?.Recenter();
                return;
            }
            if (c.ResetView)
            {
                assist?.Cancel();
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
            // World direction of this frame's ana/kata travel, which tells the settle which axis to settle along.
            Vec4 anaKataDirection = Vec4.Zero;
            if (c.AnaKata != 0f)
            {
                player.MoveAnaKata(c.AnaKata * speeds.AnaKata * scale * dt, nav.AnaKataFrame);
                Vec4 ana = nav.AnaKataFrame == WMoveFrame.View ? player.Orientation.Apply(Vec4.UnitW) : Vec4.UnitW;
                anaKataDirection = ana * Mathf.Sign(c.AnaKata);
            }
            if (c.Fly != 0f)
                player.MoveWorld(new Vec4(0f, c.Fly * speeds.Fly * scale * dt, 0f, 0f));

            // §7B rotate. W turns step when asked to, so every press lands on a nameable orientation.
            if (nav.WRotationMode == RotationMode.Snap && assist != null)
            {
                assist.StepTurn(player, Plane4.XW, c.XWPress, nav);
                assist.StepTurn(player, Plane4.YW, c.YWPress, nav);
                assist.StepTurn(player, Plane4.ZW, c.ZWPress, nav);
            }
            else
            {
                float step = speeds.Rotate * Mathf.Deg2Rad * scale * dt;
                if (c.XW != 0f) player.RotateLocal(Plane4.XW, c.XW * step);
                if (c.YW != 0f) player.RotateLocal(Plane4.YW, c.YW * step);
                if (c.ZW != 0f) player.RotateLocal(Plane4.ZW, c.ZW * step);
            }
            float turn = ComfortTurn.Step(c.XZ, c.XZPress, speeds.Rotate * scale, comfort ?? DefaultComfort, dt);
            if (turn != 0f) player.RotateLocal(Plane4.XZ, turn);
            if (c.Align)
                assist?.Align(player, nav);

            // §7C focus.
            float focusDelta = c.Focus * speeds.Focus * scale * dt + c.FocusStep;
            if (focusDelta != 0f)
                player.AdjustFocusW(focusDelta);

            assist?.Update(player, anaKataDirection, cellSize, nav, dt);
        }
    }
}
