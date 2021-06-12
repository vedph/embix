namespace Embix.Plugin.Greek
{
    /// <summary>
    /// Options for <see cref="GrcRomanizer"/>.
    /// </summary>
    public class GrcRomanizerOptions
    {
        /// <summary>
        /// Gets or sets a value indicating whether to transliterate ksi with
        /// x instead of ks.
        /// </summary>
        public bool KsiAsX { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether subscript iota should be
        /// transliterated as i instead of being dropped.
        /// </summary>
        public bool IncludeIpogegrammeni { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether gamma + velar plosive
        /// should be transliterated as n rather than g.
        /// </summary>
        public bool GammaPlusVelarAsN { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether h should be inserted after
        /// rr in transliteration (type Pyrrhus).
        /// </summary>
        public bool HAfterRR { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether khi should be rendered
        /// with ch rather than kh.
        /// </summary>
        public bool KhiAsCh { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether specific Greek punctuation,
        /// Greek ano teleia and Greek question mark, should be rendered to ;
        /// (conventionally, as we could not distinguish between ; and :) and ?,
        /// or rather just "copied" as middle dot and semicolon.
        /// </summary>
        public bool ConvertPunctuation { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the romanizer should use
        /// the default target table limited to ISO Latin only.
        /// </summary>
        public bool IsIsoLatinOnly { get; set; }

        /// <summary>
        /// Gets or sets the ID of the target conversion table used to customize
        /// the transliterator output. Leave it null to use default. You can
        /// specify a file path, or the ID of an internal table prefixed by
        /// <c>$</c>.
        /// </summary>
        public string TargetTable { get; set; }
    }
}
