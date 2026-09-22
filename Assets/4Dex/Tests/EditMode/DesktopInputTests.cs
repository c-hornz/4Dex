using System;
using FourDex.Runtime;
using NUnit.Framework;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace FourDex.Tests.EditMode
{
    /// <summary>
    /// Key-to-command bindings, driven through a simulated keyboard. Covers the half of desktop mode that
    /// DesktopControlsTests cannot: whether each key actually produces its command.
    /// </summary>
    public class DesktopInputTests : InputTestFixture
    {
        private Keyboard _keyboard;

        public override void Setup()
        {
            base.Setup();
            _keyboard = InputSystem.AddDevice<Keyboard>();
        }

        private DesktopCommands Held(Func<Keyboard, KeyControl> key)
        {
            Press(key(_keyboard));
            DesktopCommands c = DesktopTestMode.ReadDevices(_keyboard, null, false, 0f, 0f);
            Release(key(_keyboard));
            return c;
        }

        [Test]
        public void R_IsAna_F_IsKata()
        {
            Assert.AreEqual(1f, Held(k => k.rKey).AnaKata);
            Assert.AreEqual(-1f, Held(k => k.fKey).AnaKata);
        }

        [Test]
        public void EveryBindingProducesItsCommand()
        {
            Assert.AreEqual(1f, Held(k => k.wKey).Walk.y);
            Assert.AreEqual(-1f, Held(k => k.sKey).Walk.y);
            Assert.AreEqual(-1f, Held(k => k.aKey).Walk.x);
            Assert.AreEqual(1f, Held(k => k.dKey).Walk.x);
            Assert.AreEqual(1f, Held(k => k.lKey).XW);
            Assert.AreEqual(-1f, Held(k => k.jKey).XW);
            Assert.AreEqual(1f, Held(k => k.oKey).YW);
            Assert.AreEqual(-1f, Held(k => k.uKey).YW);
            Assert.AreEqual(1f, Held(k => k.iKey).ZW);
            Assert.AreEqual(-1f, Held(k => k.kKey).ZW);
            Assert.AreEqual(1f, Held(k => k.leftArrowKey).XZ);
            Assert.AreEqual(-1f, Held(k => k.rightArrowKey).XZ);
            Assert.AreEqual(1f, Held(k => k.rightBracketKey).Focus);
            Assert.AreEqual(-1f, Held(k => k.leftBracketKey).Focus);
        }

        [Test]
        public void AnaKataKeys_ProduceNoOtherCommand()
        {
            DesktopCommands c = Held(k => k.rKey);
            Assert.AreEqual(0f, c.Focus);
            Assert.AreEqual(0f, c.XW + c.YW + c.ZW + c.XZ);
            Assert.AreEqual(0f, c.Walk.sqrMagnitude);
        }

        [Test]
        public void Monitor_ShowsHeldKeyAndItsCommand()
        {
            Press(_keyboard.rKey);
            DesktopCommands c = DesktopTestMode.ReadDevices(_keyboard, null, false, 0f, 0f);

            string text = DesktopTestMode.DescribeInput(_keyboard, c);

            StringAssert.Contains("KEYS  R", text);
            StringAssert.Contains("anaKata +", text);
        }

        [Test]
        public void Monitor_SaysNone_WhenIdle()
        {
            string text = DesktopTestMode.DescribeInput(_keyboard, default);
            StringAssert.Contains("KEYS  (none)", text);
            StringAssert.Contains("CMD  (none)", text);
        }
    }
}
