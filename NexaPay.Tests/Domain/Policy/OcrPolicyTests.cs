using FluentAssertions;
using NexaPay.Domain.Policy;
using NUnit.Framework;

namespace NexaPay.Tests.Domain.Policy
{
    [TestFixture]
    [Category("Domain")]
    public class OcrPolicyTests
    {
        // 12345674 och 1234566 har korrekt mod-10-kontrollsiffra (Luhn).
        [TestCase("12345674")]
        [TestCase("1234566")]
        public void IsValid_WithCorrectCheckDigit_ReturnsTrue(string ocr)
        {
            OcrPolicy.IsValid(ocr).Should().BeTrue();
        }

        // 12345675 har fel kontrollsiffra (giltigt vore 12345674).
        [TestCase("12345675")]
        [TestCase("1234567")]
        public void IsValid_WithWrongCheckDigit_ReturnsFalse(string ocr)
        {
            OcrPolicy.IsValid(ocr).Should().BeFalse();
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("   ")]
        [TestCase("12a45674")]
        [TestCase("1")]
        public void IsValid_WithMalformedInput_ReturnsFalse(string? ocr)
        {
            OcrPolicy.IsValid(ocr).Should().BeFalse();
        }
    }
}
