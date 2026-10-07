using System;
using System.Diagnostics;
using System.Linq;
using FourDex.Math4D;
using FourDex.Runtime;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace FourDex.EditorTools
{
    /// <summary>
    /// Times the per-frame CPU work on a scene, so "it lags" can be split into 4D maths, debug panels and rendering
    /// before anything is optimised.
    /// </summary>
    /// <remarks>
    /// <para>
    /// CPU only, and deliberately so. An earlier version of this timed <c>camera.Render()</c> as well, which was
    /// worse than useless: forcing each frame to finish needs a readback that costs more than the scene does, and an
    /// empty scene "rendered" in 8.9 ms. Frame cost is measured where it happens instead, by the HUD's FRAME line,
    /// which reports the CPU and GPU halves from inside a real Play session.
    /// </para>
    /// <para>
    /// Batch: <c>-executeMethod FourDex.EditorTools.Perf4D.Benchmark -perfScene Rooms4D</c> (graphics needed, so no
    /// <c>-nographics</c>), or the <b>4Dex > Benchmark Rooms4D</b> menu item. Edit mode runs no OnEnable, so
    /// everything is registered and ticked by hand, the same way <see cref="Lab4DSceneBuilder.Snapshot"/> does.
    /// </para>
    /// </remarks>
    public static class Perf4D
    {
        /// <summary>Batch entry point: reads <c>-perfScene</c> and <c>-perfIterations</c>, then exits.</summary>
        public static void Benchmark()
        {
            string[] args = Environment.GetCommandLineArgs();
            int i = Array.IndexOf(args, "-perfScene");
            Run(i >= 0 && i + 1 < args.Length ? args[i + 1] : "Rooms4D", IntArg(args, "-perfIterations", 200));
            EditorApplication.Exit(0);
        }

        /// <summary>Rooms4D is the heaviest scene at 301 objects, so it is the one worth a menu item.</summary>
        [MenuItem("4Dex/Benchmark Rooms4D")]
        public static void BenchmarkRooms()
        {
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                Run("Rooms4D", 200);
        }

        private static void Run(string sceneName, int iterations)
        {
            EditorSceneManager.OpenScene($"Assets/Scenes/{sceneName}.unity", OpenSceneMode.Single);

            var registry = UnityEngine.Object.FindAnyObjectByType<World4DRegistry>();
            var driver = UnityEngine.Object.FindAnyObjectByType<ProjectionDriver>();
            var hud = UnityEngine.Object.FindAnyObjectByType<CoordinateHUD>();
            var slice = UnityEngine.Object.FindAnyObjectByType<WSliceViewer>();

            Object4DBehaviour[] objects = UnityEngine.Object.FindObjectsByType<Object4DBehaviour>(FindObjectsSortMode.None);
            foreach (Object4DBehaviour o in objects)
                registry.Register(o);

            int renderers = objects.Count(o => o.GetComponent<PhaseRenderer>() != null);
            Debug.Log($"[perf] {sceneName}: {objects.Length} 4D objects, {renderers} with PhaseRenderer, {iterations} iterations");

            Report("ProjectAll (shadows on)", iterations, () => driver.ProjectAll());

            PerceptionSettings perception = driver.Perception;
            if (perception != null)
            {
                bool hadShadows = perception.Shadow.Enabled;
                perception.Shadow.Enabled = false;
                Report("ProjectAll (shadows off)", iterations, () => driver.ProjectAll());
                perception.Shadow.Enabled = hadShadows;
            }

            if (hud != null)
                Report("CoordinateHUD.Refresh (10 Hz)", Math.Min(iterations, 100), () => hud.Refresh());
            if (slice != null)
                Report("WSliceViewer.Refresh (20 Hz)", Math.Min(iterations, 100), () => slice.Refresh());

            // The navigation work put this in the HUD: a search over all 192 axis-aligned orientations, run twice per
            // refresh while off-axis.
            Rotor4 turned = Rotor4.FromPlane(Plane4.XW, 0.7f).Then(Rotor4.FromPlane(Plane4.YZ, 0.3f));
            Report("AxisAlignment.Nearest", 1000, () => AxisAlignment.Nearest(turned));
        }

        private static void Report(string label, int iterations, Action work)
        {
            for (int i = 0; i < Math.Max(iterations / 10, 5); i++) // warm up: the first call pays for lazy setup
                work();

            var sw = Stopwatch.StartNew();
            double worst = 0d;
            for (int i = 0; i < iterations; i++)
            {
                double at = sw.Elapsed.TotalMilliseconds;
                work();
                worst = Math.Max(worst, sw.Elapsed.TotalMilliseconds - at);
            }
            sw.Stop();

            double mean = sw.Elapsed.TotalMilliseconds / iterations;
            // 11.1 ms is the whole frame at 90 Hz, which is what the Rift needs.
            Debug.Log($"[perf] {label,-30} mean {mean,8:0.000} ms   worst {worst,8:0.000} ms   " +
                      $"{mean / 11.1d * 100d,5:0.0}% of a 90 Hz frame");
        }

        private static int IntArg(string[] args, string name, int fallback)
        {
            int i = Array.IndexOf(args, name);
            return i >= 0 && i + 1 < args.Length && int.TryParse(args[i + 1], out int v) ? v : fallback;
        }
    }
}
