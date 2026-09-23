/*	
 * 	
 *  This file is part of libfintx.
 *  
 *  Copyright (C) 2016 - 2022 Torsten Klinger
 * 	E-Mail: torsten.klinger@googlemail.com
 *  
 *  This program is free software; you can redistribute it and/or
 *  modify it under the terms of the GNU Lesser General Public
 *  License as published by the Free Software Foundation; either
 *  version 3 of the License, or (at your option) any later version.
 *
 *  This program is distributed in the hope that it will be useful,
 *  but WITHOUT ANY WARRANTY; without even the implied warranty of
 *  MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU
 *  Lesser General Public License for more details.
 *
 *  You should have received a copy of the GNU Lesser General Public License
 *  along with this program; if not, write to the Free Software Foundation,
 *  Inc., 51 Franklin Street, Fifth Floor, Boston, MA  02110-1301, USA.
 * 	
 */

using System;
using System.Linq;

namespace libfintx.FinTS
{
    /// <summary>
    /// Softics fork: the pain descriptor of a SEPA direct debit, and the segment version to
    /// use for it.
    /// </summary>
    /// <remarks>
    /// Upstream hard-codes <c>pain.008.002.02</c> in HKDSE and HKDME. German banks withdrew
    /// that version in November 2025 and switch it off on 14 November 2026, so the hard-coded
    /// value turns every collection into a rejection - and the rejection arrives after the
    /// message has been sent. This class is the single place where the descriptor is decided.
    /// </remarks>
    internal static class PainDescriptor
    {
        /// <summary>The version German banks expect today.</summary>
        internal const string DirectDebitCurrent = "pain.008.001.08";

        private const string UrnPrefix = "urn:iso:std:iso:20022:tech:xsd:";

        /// <summary>
        /// The descriptor to put into the segment: what the caller asked for, else what the
        /// bank announced in HISPAS, else the current version. Never the withdrawn one by
        /// default.
        /// </summary>
        internal static string Resolve(FinTsClient client, string requested)
        {
            if (!string.IsNullOrWhiteSpace(requested))
                return requested;

            if (client != null && !string.IsNullOrWhiteSpace(client.HISPAS_PainDirectDebit))
                return client.HISPAS_PainDirectDebit;

            return DirectDebitCurrent;
        }

        /// <summary>The descriptor as an escaped HBCI data element, urn included.</summary>
        internal static string Escaped(string descriptor)
        {
            var value = string.IsNullOrWhiteSpace(descriptor) ? DirectDebitCurrent : descriptor;

            if (value.IndexOf("urn:", StringComparison.Ordinal) < 0)
                value = UrnPrefix + value;

            return Helper.EscapeHbciString(value);
        }

        /// <summary>
        /// Puts a ready-made pain message into the binary data element of a segment that ends
        /// in the placeholder <c>@@</c>, and terminates the segment.
        /// </summary>
        /// <remarks>
        /// Upstream writes <c>"@" + (message.Length - 1) + "@" + message</c>. That is only right
        /// because upstream's own pain generators end their message with the segment terminator
        /// <c>'</c>: the <c>- 1</c> keeps the terminator out of the binary length, and the
        /// terminator itself closes the segment (<see cref="SEG.toSEG"/> does not). A caller
        /// passing a plain XML document - which is what a pain message is - lost its last
        /// character <c>&gt;</c> to the segment body and left the segment unterminated. Banks
        /// answer that with 9050 "Teilweise fehlerhaft" and 9160 "Nicht belegt", after the
        /// message has been sent.
        ///
        /// A trailing terminator is accepted and dropped, so a message built by upstream's
        /// generators still works. The length counts characters, which equals bytes here:
        /// <c>FinTSMessage</c> encodes the whole message as ASCII, one byte per character.
        /// </remarks>
        internal static string AttachPayload(string segment, string painXml)
        {
            if (segment == null || !segment.EndsWith("@@", StringComparison.Ordinal))
                throw new ArgumentException("The segment must end in the placeholder @@.", nameof(segment));

            if (string.IsNullOrWhiteSpace(painXml))
                throw new ArgumentException("A collection needs a payload.", nameof(painXml));

            var payload = painXml.EndsWith("'", StringComparison.Ordinal)
                ? painXml.Substring(0, painXml.Length - 1)
                : painXml;

            return segment.Substring(0, segment.Length - 2) + "@" + payload.Length + "@" + payload + "'";
        }

        /// <summary>
        /// The highest segment version the bank announced for the given BPD segment, capped at
        /// <paramref name="highest"/>; <paramref name="fallback"/> when the BPD says nothing.
        /// </summary>
        /// <remarks>
        /// Reading it beats guessing it: the commented-out lines in HKDME show that upstream
        /// wavered between version 1 and 2, and the two versions carry different data elements.
        /// </remarks>
        internal static int SegmentVersion(FinTsClient client, string name, int fallback, int highest)
        {
            var bpd = client == null ? null : client.BPD;

            if (bpd == null || bpd.SegmentList == null)
                return fallback;

            var announced = bpd.SegmentList
                .Where(s => s != null && s.Name == name && s.Version <= highest)
                .Select(s => s.Version)
                .DefaultIfEmpty(0)
                .Max();

            return announced > 0 ? announced : fallback;
        }
    }
}
