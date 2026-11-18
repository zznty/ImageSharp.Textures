using Xunit.Abstractions;
using Xunit.Sdk;

namespace ImageSharp.Textures.Parity.Tests.TestUtilities;

/// <summary>
///     Marks every test case discovered through <see cref="TexconvTheoryAttribute" /> as skipped when texconv is
///     not available.
/// </summary>
public sealed class TexconvTheoryDiscoverer : TheoryDiscoverer
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="TexconvTheoryDiscoverer" /> class.
    /// </summary>
    /// <param name="diagnosticMessageSink">The message sink used to report diagnostics.</param>
    public TexconvTheoryDiscoverer(IMessageSink diagnosticMessageSink)
        : base(diagnosticMessageSink)
    {
    }

    /// <inheritdoc />
    public override IEnumerable<IXunitTestCase> Discover(
        ITestFrameworkDiscoveryOptions discoveryOptions,
        ITestMethod testMethod,
        IAttributeInfo theoryAttribute)
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

        return base.Discover(discoveryOptions, testMethod, theoryAttribute);
    }
}
