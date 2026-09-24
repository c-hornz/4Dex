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
    }
}
