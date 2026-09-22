using FourDex.InputMapping;
using UnityEngine;

namespace FourDex.Runtime
{
    /// <summary>
    /// One mapping scheme as an asset (SPEC §6: schemes are configuration, not scripts). Edit bindings and weights in
    /// the inspector, live during play; like PerceptionSettings, edits persist in the asset.
    /// </summary>
    [CreateAssetMenu(fileName = "MappingProfile", menuName = "4Dex/Input Mapping Profile")]
    public sealed class InputMappingProfile : ScriptableObject
    {
        public MappingProfile Profile = new MappingProfile();
    }
}
