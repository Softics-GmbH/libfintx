using System;
using libfintx.FinTS;
using Xunit;

namespace libfintx.Tests.Segments;

/// <summary>
/// Softics fork: the binary data element that carries a ready-made pain message.
/// </summary>
/// <remarks>
/// Until 1.4.0-softics.2 the pre-built payload overloads announced one character too few and
/// did not terminate the segment. Banks rejected every collection with 9050/9160.
/// </remarks>
public class PainDescriptorTests
{
    private const string Segment = "HKDSE:4:1+DE14660702130071817100:DEUTDESMP12+urn?:iso?:std?:iso?:20022?:tech?:xsd?:pain.008.001.08+@@";

    private const string Xml = "<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n<Document>\n  <Nm>O'Brien</Nm>\n</Document>";

    [Fact]
    public void A_plain_xml_document_is_announced_with_its_full_length_and_the_segment_is_terminated()
    {
        var result = PainDescriptor.AttachPayload(Segment, Xml);

        Assert.Equal(
            Segment.Substring(0, Segment.Length - 2) + "@" + Xml.Length + "@" + Xml + "'",
            result);
    }

    [Fact]
    public void The_announced_length_covers_exactly_the_xml_up_to_its_last_character()
    {
        var result = PainDescriptor.AttachPayload(Segment, Xml);

        var head = Segment.Substring(0, Segment.Length - 2) + "@";
        var lengthEnd = result.IndexOf('@', head.Length);
        var length = int.Parse(result.Substring(head.Length, lengthEnd - head.Length));
        var binary = result.Substring(lengthEnd + 1, length);

        Assert.Equal(Xml, binary);
        Assert.Equal("'", result.Substring(lengthEnd + 1 + length));
    }

    [Fact]
    public void A_payload_that_already_ends_in_the_terminator_is_not_terminated_twice()
    {
        // Upstream's own pain generators end their message with "'".
        var result = PainDescriptor.AttachPayload(Segment, Xml + "'");

        Assert.Equal(PainDescriptor.AttachPayload(Segment, Xml), result);
    }

    [Fact]
    public void A_segment_without_the_placeholder_is_refused()
    {
        Assert.Throws<ArgumentException>(() => PainDescriptor.AttachPayload("HKDSE:4:1+x+", Xml));
    }

    [Fact]
    public void An_empty_payload_is_refused()
    {
        Assert.Throws<ArgumentException>(() => PainDescriptor.AttachPayload(Segment, " "));
    }
}
