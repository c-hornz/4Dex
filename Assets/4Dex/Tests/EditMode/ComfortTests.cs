using FourDex.Math4D;
using FourDex.Player;
using FourDex.Runtime;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace FourDex.Tests.EditMode
{
    /// <summary>SPEC §22 in the scene: the vignette responds to the player's own motion, and turning can be snapped.</summary>
    public class ComfortTests
    {
        private GameObject _camera;
        private Player4D _player;
        private ComfortSettings _settings;
        private ComfortVignette _vignette;

        [SetUp]
        public void SetUp()
        {
            _camera = new GameObject("Camera", typeof(Camera));
            _player = new GameObject("Player").AddComponent<Player4D>();
            _settings = ScriptableObject.CreateInstance<ComfortSettings>();
            _vignette = _camera.AddComponent<ComfortVignette>();
            var so = new SerializedObject(_vignette);
            so.FindProperty("_player").objectReferenceValue = _player;
            so.FindProperty("_settings").objectReferenceValue = _settings;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_camera);
            Object.DestroyImmediate(_player.gameObject);
            Object.DestroyImmediate(_settings);
        }

        [Test]
        public void SnapTurn_StepsOnPress_WhileSmoothTurnSweeps()
        {
            var state = new Player4DState();
            var smooth = new ComfortParameters { TurnMode = TurnMode.Smooth }.Validated();
            var speeds = DesktopSpeeds.Default;

            // Held, smooth: turning follows the control.
            DesktopControls.Apply(state, null, Quaternion.identity, new DesktopCommands { XZ = 1f }, speeds, 1f, null, smooth);
            Assert.AreEqual(speeds.Rotate, state.Orientation.Log().XZ * Mathf.Rad2Deg, 0.01f);

            // Held, snap: nothing happens until a press arrives.
            var snap = new ComfortParameters { TurnMode = TurnMode.Snap, SnapDegrees = 30f }.Validated();
            var stepped = new Player4DState();
            DesktopControls.Apply(stepped, null, Quaternion.identity, new DesktopCommands { XZ = 1f }, speeds, 1f, null, snap);
            Assert.AreEqual(0f, stepped.Orientation.Log().XZ, 1e-6f);

            DesktopControls.Apply(stepped, null, Quaternion.identity, new DesktopCommands { XZPress = 1 }, speeds, 1f, null, snap);
            Assert.AreEqual(30f, stepped.Orientation.Log().XZ * Mathf.Rad2Deg, 0.01f);
        }

        [Test]
        public void SnapTurn_DoesNotQuantiseTheWRotations()
        {
            // Comfort snap is for ordinary turning only. Stepping the W turns is a navigation setting of its own.
            var state = new Player4DState();
            var snap = new ComfortParameters { TurnMode = TurnMode.Snap }.Validated();
            DesktopControls.Apply(state, null, Quaternion.identity, new DesktopCommands { XW = 1f },
                DesktopSpeeds.Default, 1f, null, snap);
            Assert.AreEqual(DesktopSpeeds.Default.Rotate, state.Orientation.Log().XW * Mathf.Rad2Deg, 0.01f);
        }

        [Test]
        public void SteppedWTurns_StepOnPress_AndPlayOutToAQuarterTurn()
        {
            var state = new Player4DState();
            var assist = new NavigationAssist();
            var nav = new NavigationParameters { WRotationMode = RotationMode.Snap, WSnapDegrees = 90f, TurnSeconds = 0.2f };
            var speeds = DesktopSpeeds.Default;

            // Holding does nothing in snap mode; the press starts the turn, and time plays it out.
            DesktopControls.Apply(state, null, Quaternion.identity, new DesktopCommands { XW = 1f }, speeds, 0.1f,
                null, null, assist, nav);
            Assert.IsTrue(state.Orientation.ApproxEquals(Rotor4.Identity));
            DesktopControls.Apply(state, null, Quaternion.identity, new DesktopCommands { XW = 1f, XWPress = 1 }, speeds, 0.1f,
                null, null, assist, nav);
            DesktopControls.Apply(state, null, Quaternion.identity, new DesktopCommands { XW = 1f }, speeds, 0.5f,
                null, null, assist, nav);

            Assert.IsTrue(state.Orientation.ApproxEquals(Rotor4.FromPlane(Plane4.XW, Mathf.PI / 2f), 1e-5f));
        }

        [Test]
        public void AnaKata_InTheViewFrame_FollowsWhatIsShownAsAna()
        {
            var nav = new NavigationParameters { AnaKataFrame = WMoveFrame.View, SettleToSlices = false };
            var state = new Player4DState(new Transform4(Vec4.Zero, Rotor4.FromPlane(Plane4.XW, Mathf.PI)));

            DesktopControls.Apply(state, null, Quaternion.identity, new DesktopCommands { AnaKata = 1f },
                DesktopSpeeds.Default, 1f, null, null, new NavigationAssist(), nav);

            // Turned half way round through W, R travels world kata, which is the player's own ana.
            Assert.AreEqual(-DesktopSpeeds.Default.AnaKata, state.Position.W, 1e-4f);
            Assert.AreEqual(DesktopSpeeds.Default.AnaKata, state.Pose.InverseTransformDirection(state.Position).W, 1e-4f);
        }

        [Test]
        public void AlignKey_TurnsToTheNearestAxisAlignedOrientation()
        {
            var nav = new NavigationParameters { TurnSeconds = 0f };
            var state = new Player4DState(new Transform4(Vec4.Zero, Rotor4.FromPlane(Plane4.YW, 1.2f)));

            DesktopControls.Apply(state, null, Quaternion.identity, new DesktopCommands { Align = true },
                DesktopSpeeds.Default, 0.016f, null, null, new NavigationAssist(), nav);

            Assert.IsTrue(state.Orientation.ApproxEquals(Rotor4.FromPlane(Plane4.YW, Mathf.PI / 2f), 1e-4f));
        }

        [Test]
        public void ThePanelExposesTheNavigationControls()
        {
            var entries = ComfortParameterList.For(_settings);
            ParameterEntry frame = entries.Find(e => e.Label.Contains("ana/kata frame"));
            Assert.AreEqual("View", frame.Display);
            frame.Adjust(+1, false);
            Assert.AreEqual(WMoveFrame.World, _settings.Navigation.AnaKataFrame);
            Assert.IsTrue(entries.Exists(e => e.Label.Contains("settle to slices")));
        }

        [Test]
        public void TheVignetteBuildsItsDisc_AndOpensWhenStill()
        {
            _vignette.Tick(1f / 60f);
            Assert.AreEqual(1f, _vignette.Aperture, 1e-4f);
            Assert.NotNull(_camera.GetComponentInChildren<MeshRenderer>(), "the disc is built on demand");
            Assert.IsFalse(_camera.GetComponentInChildren<MeshRenderer>().enabled, "and is not drawn while open");
        }

        [Test]
        public void MovingThroughW_ClosesTheVignette()
        {
            // Travelling ana is motion the inner ear has no account of at all, so it drives the vignette like walking.
            var model = new VignetteModel();
            ComfortParameters p = _settings.Parameters.Validated();
            for (int i = 0; i < 120; i++)
                model.Update(new MotionSample(0f, 0f, p.WRateReference), p, 1f / 60f);

            Assert.Less(model.Aperture, 1f - 0.5f * p.VignetteStrength);
        }

        [Test]
        public void ThePanelExposesTheComfortControls()
        {
            var entries = ComfortParameterList.For(_settings);
            Assert.IsTrue(entries.Exists(e => e.Label.Contains("vignette")));

            ParameterEntry mode = entries.Find(e => e.Label.Contains("turn mode"));
            Assert.AreEqual("Smooth", mode.Display);
            mode.Adjust(+1, false);
            Assert.AreEqual(TurnMode.Snap, _settings.Parameters.TurnMode);
        }
    }
}
