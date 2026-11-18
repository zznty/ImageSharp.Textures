using Xunit.Abstractions;
using Xunit.Sdk;

namespace ImageSharp.Textures.Parity.Tests.TestUtilities;

/// <summary>
///     A test case that is reported as skipped without being run.
/// </summary>
/// <remarks>
///     xunit v2 exposes the skip reason only through an internal setter, so the parity suite derives a test case
///     that carries it. Discovery returns this case instead of the real one when texconv is missing, which keeps
///     the skip visible in every runner and in the counts.
/// </remarks>
internal sealed class SkippedTestCase : XunitTestCase
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="SkippedTestCase" /> class for the deserializer.
    /// </summary>
    [Obsolete("Called by the de-serializer.", false)]
    public SkippedTestCase()
    {
    }

    /// <summary>
    ///     Initializes a new instance of the <see cref="SkippedTestCase" /> class.
    /// </summary>
    /// <param name="diagnosticMessageSink">The message sink used to report diagnostics.</param>
    /// <param name="defaultMethodDisplay">The default method display.</param>
    /// <param name="defaultMethodDisplayOptions">The default method display options.</param>
    /// <param name="testMethod">The test method.</param>
    /// <param name="skipReason">The skip reason.</param>
    public SkippedTestCase(
        IMessageSink diagnosticMessageSink,
        TestMethodDisplay defaultMethodDisplay,
        TestMethodDisplayOptions defaultMethodDisplayOptions,
        ITestMethod testMethod,
        string skipReason)
        : base(diagnosticMessageSink, defaultMethodDisplay, defaultMethodDisplayOptions, testMethod)
        => this.SkipReason = skipReason;

    /// <inheritdoc />
    public override void Serialize(IXunitSerializationInfo data)
    {
        base.Serialize(data);
        data.AddValue(nameof(this.SkipReason), this.SkipReason);
    }

    /// <inheritdoc />
    public override void Deserialize(IXunitSerializationInfo data)
    {
        base.Deserialize(data);
        this.SkipReason = data.GetValue<string>(nameof(this.SkipReason));
    }
}
