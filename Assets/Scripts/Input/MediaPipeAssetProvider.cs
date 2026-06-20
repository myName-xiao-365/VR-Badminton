using System.Collections;

#if VRBADMINTON_MEDIAPIPE
using Mediapipe.Unity;
#endif

namespace VRBadminton.Input
{
    internal interface IMediaPipeAssetProvider
    {
        string Name { get; }

        IEnumerator PrepareAssetAsync(string modelAssetPath);
    }

    internal static class MediaPipeAssetProviderFactory
    {
        public static IMediaPipeAssetProvider CreateDefault()
        {
#if UNITY_EDITOR
            return CreateEditorPackageProvider();
#else
            return CreateStreamingAssetsProvider();
#endif
        }

        public static IMediaPipeAssetProvider CreateEditorPackageProvider()
        {
            return new PackageResourceMediaPipeAssetProvider();
        }

        public static IMediaPipeAssetProvider CreateStreamingAssetsProvider()
        {
            return new StreamingAssetsMediaPipeAssetProvider();
        }
    }

    internal sealed class PackageResourceMediaPipeAssetProvider : IMediaPipeAssetProvider
    {
        public string Name => "PackageResources";

        public IEnumerator PrepareAssetAsync(string modelAssetPath)
        {
#if VRBADMINTON_MEDIAPIPE && UNITY_EDITOR
            IResourceManager resourceManager = new LocalResourceManager("VRBadminton/MediaPipe");
            return resourceManager.PrepareAssetAsync(modelAssetPath, modelAssetPath, overwriteDestination: false);
#else
            return Empty();
#endif
        }

        private static IEnumerator Empty()
        {
            yield break;
        }
    }

    internal sealed class StreamingAssetsMediaPipeAssetProvider : IMediaPipeAssetProvider
    {
        public string Name => "StreamingAssets";

        public IEnumerator PrepareAssetAsync(string modelAssetPath)
        {
#if VRBADMINTON_MEDIAPIPE
            IResourceManager resourceManager = new StreamingAssetsResourceManager("VRBadminton/MediaPipe");
            return resourceManager.PrepareAssetAsync(modelAssetPath, modelAssetPath, overwriteDestination: false);
#else
            return Empty();
#endif
        }

        private static IEnumerator Empty()
        {
            yield break;
        }
    }
}
