using Xunit.Abstractions;
using Xunit.Sdk;

namespace ImageSharp.Textures.Parity.Tests.TestUtilities;

/// <summary>
///     Marks every test case discovered through <see cref="TexconvFactAttribute" /> as skipped when texconv is
///     not available, which is the only way xunit v2 can skip at discovery time.
/// </summary>
public sealed class TexconvFactDiscoverer : FactDiscoverer
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="TexconvFactDiscoverer" /> class.
    /// </summary>
    /// <param name="diagnosticMessageSink">The message sink used to report diagnostics.</param>
    public TexconvFactDiscoverer(IMessageSink diagnosticMessageSink)
        : base(diagnosticMessageSink)
    {
    }

    /// <inheritdoc />
    public override IEnumerable<IXunitTestCase> Discover(
        ITestFrameworkDiscoveryOptions discoveryOptions,
        ITestMethod testMethod,
        IAttributeInfo factAttribute)
    {
        if (!TexconvLocator.IsAvailable)
        {
            return new IXunitTestCase[]
            {
                new SkippedTestCase(
                    this.DiagnosticMessageSink,
                    discoveryOptions.MethodDisplayOrDefault(),
                    discoveryOptions.MethodDisplayOptionsOrDefault(),
                    testMethod,
                    TexconvLocator.SkipReason)
            };
        }

        return base.Discover(discoveryOptions, testMethod, factAttribute);
    }
}
