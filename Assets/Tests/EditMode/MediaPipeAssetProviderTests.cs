using NUnit.Framework;
using VRBadminton.Input;

namespace VRBadminton.Tests
{
    public sealed class MediaPipeAssetProviderTests
    {
        [Test]
        public void FactoryExposesEditorAndStreamingAssetsProviders()
        {
            IMediaPipeAssetProvider editorProvider =
                MediaPipeAssetProviderFactory.CreateEditorPackageProvider();
            IMediaPipeAssetProvider streamingProvider =
                MediaPipeAssetProviderFactory.CreateStreamingAssetsProvider();

            Assert.AreEqual("PackageResources", editorProvider.Name);
            Assert.AreEqual("StreamingAssets", streamingProvider.Name);
            Assert.IsInstanceOf<PackageResourceMediaPipeAssetProvider>(editorProvider);
            Assert.IsInstanceOf<StreamingAssetsMediaPipeAssetProvider>(streamingProvider);
        }
    }
}
