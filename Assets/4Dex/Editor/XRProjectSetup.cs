using System.IO;
using UnityEditor;
using UnityEditor.XR.Management;
using UnityEditor.XR.Management.Metadata;
using UnityEditor.XR.OpenXR.Features;
using UnityEngine;
using UnityEngine.XR.Management;
using UnityEngine.XR.OpenXR.Features.Interactions;

namespace FourDex.EditorTools
{
    /// <summary>
    /// Configures XR for Rift PCVR: the OpenXR loader on Standalone, plus the Oculus Touch interaction profile.
    /// Idempotent, so it is safe to rerun. Batch mode:
    /// <c>Unity.exe -batchmode -quit -projectPath . -executeMethod FourDex.EditorTools.XRProjectSetup.Configure</c>
    /// </summary>
    /// <remarks>
    /// Kept in the repo so the XR configuration is reproducible from source rather than from clicks in
    /// Project Settings. Uses OpenXR rather than the Oculus XR plugin so hardware stays behind a standard
    /// abstraction (SPEC §2). XR Interaction Toolkit is deliberately absent: 4Dex reads raw input (ARCHITECTURE §11).
    /// </remarks>
    public static class XRProjectSetup
    {
        private const BuildTargetGroup Target = BuildTargetGroup.Standalone;
        private const string OpenXRLoader = "UnityEngine.XR.OpenXR.OpenXRLoader";
        private const string SettingsFolder = "Assets/XR";

        [MenuItem("4Dex/Configure XR (OpenXR, Rift PCVR)")]
        public static void Configure()
        {
            XRGeneralSettingsPerBuildTarget perTarget = GetOrCreatePerTargetSettings();

            if (!perTarget.HasSettingsForBuildTarget(Target))
                perTarget.CreateDefaultSettingsForBuildTarget(Target);
            if (!perTarget.HasManagerSettingsForBuildTarget(Target))
                perTarget.CreateDefaultManagerSettingsForBuildTarget(Target);

            XRGeneralSettings general = perTarget.SettingsForBuildTarget(Target);
            general.InitManagerOnStart = true;

            XRManagerSettings manager = perTarget.ManagerSettingsForBuildTarget(Target);
            if (!XRPackageMetadataStore.IsLoaderAssigned(OpenXRLoader, Target) &&
                !XRPackageMetadataStore.AssignLoader(manager, OpenXRLoader, Target))
            {
                Debug.LogError("[4Dex] Could not assign the OpenXR loader for Standalone.");
                return;
            }

            var touch = FeatureHelpers.GetFeatureWithIdForBuildTarget(Target, OculusTouchControllerProfile.featureId);
            if (touch == null)
            {
                Debug.LogError("[4Dex] Oculus Touch interaction profile not found; is com.unity.xr.openxr installed?");
                return;
            }
            touch.enabled = true;

            EditorUtility.SetDirty(touch);
            EditorUtility.SetDirty(general);
            EditorUtility.SetDirty(manager);
            EditorUtility.SetDirty(perTarget);
            AssetDatabase.SaveAssets();
            Debug.Log("[4Dex] XR configured: OpenXR loader on Standalone, Oculus Touch profile enabled.");
        }

        // Mirrors XR Management's internal GetOrCreate, which is not public.
        private static XRGeneralSettingsPerBuildTarget GetOrCreatePerTargetSettings()
        {
            EditorBuildSettings.TryGetConfigObject(XRGeneralSettings.settingsKey, out XRGeneralSettingsPerBuildTarget settings);
            if (settings != null)
                return settings;

            string[] found = AssetDatabase.FindAssets("t:XRGeneralSettingsPerBuildTarget");
            if (found.Length > 0)
            {
                settings = AssetDatabase.LoadAssetAtPath<XRGeneralSettingsPerBuildTarget>(AssetDatabase.GUIDToAssetPath(found[0]));
            }
            else
            {
                Directory.CreateDirectory(SettingsFolder);
                settings = ScriptableObject.CreateInstance<XRGeneralSettingsPerBuildTarget>();
                AssetDatabase.CreateAsset(settings, SettingsFolder + "/XRGeneralSettingsPerBuildTarget.asset");
                AssetDatabase.SaveAssets();
            }
            EditorBuildSettings.AddConfigObject(XRGeneralSettings.settingsKey, settings, true);
            return settings;
        }
    }
}
