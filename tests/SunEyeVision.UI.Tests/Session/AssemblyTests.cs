using Xunit;
[assembly:CollectionBehavior(DisableTestParallelization=true)]
namespace SunEyeVision.UI.Tests;
[CollectionDefinition("VM canvas UI")] public sealed class CanvasCollection { }
[CollectionDefinition("Editor commit UI")] public sealed class EditorCollection { }
