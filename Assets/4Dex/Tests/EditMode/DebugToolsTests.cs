using FourDex.Perception;
using FourDex.Runtime;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace FourDex.Tests.EditMode
{
    /// <summary>The W slice viewer's axis mapping, and the runtime parameter panel's editing rules.</summary>
    public class DebugToolsTests
    {
        [Test]
        public void SliceAxis_PutsPlayerInTheMiddle_KataLeft_AnaRight()
        {
            Assert.AreEqual(0.5f, WSliceLayout.ToUnit(0f, 2f), 1e-6f);
            Assert.AreEqual(0.75f, WSliceLayout.ToUnit(1f, 2f), 1e-6f);
            Assert.AreEqual(0.25f, WSliceLayout.ToUnit(-1f, 2f), 1e-6f);
            Assert.AreEqual(1f, WSliceLayout.ToUnit(5f, 2f));
            Assert.AreEqual(0f, WSliceLayout.ToUnit(-5f, 2f));
        }

        [Test]
        public void SliceInterval_SpansTheExtent_AndFlagsClipping()
        {
            WSliceLayout.Span slab = WSliceLayout.Interval(0f, 1.5f, 2f);
            Assert.AreEqual(0.125f, slab.Start, 1e-6f);
            Assert.AreEqual(0.875f, slab.End, 1e-6f);
            Assert.IsFalse(slab.ClippedKata || slab.ClippedAna);

            WSliceLayout.Span far = WSliceLayout.Interval(1.8f, 0.5f, 2f);
            Assert.AreEqual(1f, far.End);
            Assert.IsTrue(far.ClippedAna);
            Assert.IsFalse(far.ClippedKata);
        }

        private static PerceptionSettings NewSettings() => ScriptableObject.CreateInstance<PerceptionSettings>();

        [Test]
        public void FloatEntry_StepsAndClamps_AndCoarseIsTenSteps()
        {
            PerceptionSettings s = NewSettings();
            try
            {
                ParameterEntry width = PerceptionParameterList.For(s)[0];
                Assert.AreEqual("focusWidth", width.Label);
                float start = s.Parameters.FocusWidth;

                width.Adjust(+1, false);
                Assert.AreEqual(start + 0.01f, s.Parameters.FocusWidth, 1e-5f);
                width.Adjust(-1, true);
                Assert.AreEqual(start + 0.01f - 0.1f, s.Parameters.FocusWidth, 1e-5f);

                for (int i = 0; i < 100; i++)
                    width.Adjust(-1, true);
                Assert.AreEqual(0.01f, s.Parameters.FocusWidth, 1e-6f, "clamped at its minimum");
            }
            finally
            {
                Object.DestroyImmediate(s);
            }
        }

        [Test]
        public void EnumEntry_CyclesBothWays_AndBoolEntryToggles()
        {
            PerceptionSettings s = NewSettings();
            try
            {
                var entries = PerceptionParameterList.For(s);
                ParameterEntry curve = entries.Find(e => e.Label == "curve");
                ParameterEntry invert = entries.Find(e => e.Label == "invert phase dir");

                curve.Adjust(+1, false);
                Assert.AreEqual(VisibilityCurve.Linear, s.Parameters.Curve);
                curve.Adjust(-1, false);
                curve.Adjust(-1, false);
                Assert.AreEqual(VisibilityCurve.Hard, s.Parameters.Curve, "wraps from the first to the last");

                Assert.AreEqual("off", invert.Display);
                invert.Adjust(+1, false);
                Assert.IsTrue(s.Parameters.InvertPhaseDirection);
                Assert.AreEqual("on", invert.Display);
            }
            finally
            {
                Object.DestroyImmediate(s);
            }
        }

        [Test]
        public void Panel_SelectionWraps_AndAdjustEditsTheSelectedParameter()
        {
            PerceptionSettings s = NewSettings();
            var camera = new GameObject("Camera", typeof(Camera));
            try
            {
                var panel = camera.AddComponent<RuntimeParameterPanel>();
                var so = new SerializedObject(panel);
                so.FindProperty("_settings").objectReferenceValue = s;
                so.ApplyModifiedPropertiesWithoutUndo();

                panel.MoveSelection(-1);
                Assert.AreEqual(panel.Entries.Count - 1, panel.Selected, "up from the top wraps to the bottom");
                panel.MoveSelection(+1);
                Assert.AreEqual(0, panel.Selected);

                panel.MoveSelection(+2); // band A
                float before = s.Parameters.BandA;
                panel.Adjust(+1, false);
                Assert.AreEqual(before + 0.01f, s.Parameters.BandA, 1e-5f);
                StringAssert.Contains("> band A (LOCAL)", panel.Format());
            }
            finally
            {
                Object.DestroyImmediate(camera);
                Object.DestroyImmediate(s);
            }
        }

        [Test]
        public void Panel_Toggles_AndStartsHidden()
        {
            var camera = new GameObject("Camera", typeof(Camera));
            try
            {
                var panel = camera.AddComponent<RuntimeParameterPanel>();
                Assert.IsFalse(panel.Visible);
                panel.Toggle();
                Assert.IsTrue(panel.Visible);
                panel.Toggle();
                Assert.IsFalse(panel.Visible);
            }
            finally
            {
                Object.DestroyImmediate(camera);
            }
        }
    }
}
