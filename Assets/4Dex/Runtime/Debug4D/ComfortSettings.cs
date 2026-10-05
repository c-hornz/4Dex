using FourDex.Player;
using UnityEngine;

namespace FourDex.Runtime
{
    /// <summary>
    /// SPEC §22's comfort controls as an asset, so they can be tuned in play like everything else. Separate from
    /// PerceptionSettings on purpose: these change how bearable an experiment is, not what it shows.
    /// </summary>
    [CreateAssetMenu(fileName = "ComfortSettings", menuName = "4Dex/Comfort Settings")]
    public sealed class ComfortSettings : ScriptableObject
    {
        [Tooltip("Vignette response and snap/smooth turning. The spec allows uncomfortable mappings; these make them survivable.")]
        public ComfortParameters Parameters = new ComfortParameters();

        // Kept here rather than in an asset of its own because it answers the same need, staying oriented while the
        // experiment runs, and the scenes already wire this asset to the desktop controls and the panel.
        [Tooltip("Ana/kata frame, stepped W turns, align-to-axes and settling into W slices.")]
        public NavigationParameters Navigation = new NavigationParameters();
    }
}
