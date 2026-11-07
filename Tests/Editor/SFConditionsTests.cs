using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using SFramework.Modules.SFConditions.Runtime;

namespace SFramework.Conditions.Tests
{
    public sealed class SFConditionsTests
    {
        [TestCase(true, true, true)]
        [TestCase(true, false, false)]
        [TestCase(false, true, false)]
        [TestCase(false, false, false)]
        public void AllMetRequiresEveryConditionToSucceed(bool first, bool second, bool expected)
        {
            ISFCondition[] conditions = { new SFPackageBooleanCondition { Value = first }, new SFPackageBooleanCondition { Value = second } };

            Assert.That(conditions.AllMet(), Is.EqualTo(expected));
        }

        [TestCase(true, true, true)]
        [TestCase(true, false, true)]
        [TestCase(false, true, true)]
        [TestCase(false, false, false)]
        public void AnyMetAcceptsAtLeastOneSuccessfulCondition(bool first, bool second, bool expected)
        {
            ISFCondition[] conditions = { new SFPackageBooleanCondition { Value = first }, new SFPackageBooleanCondition { Value = second } };

            Assert.That(conditions.AnyMet(), Is.EqualTo(expected));
        }

        [Test]
        public void AllMetStopsAfterFirstFailure()
        {
            var trace = new List<string>();
            ISFCondition[] conditions =
            {
                new SFPackageRecordingCondition("first", true, trace),
                new SFPackageRecordingCondition("failure", false, trace),
                new SFPackageRecordingCondition("skipped", true, trace)
            };

            Assert.That(conditions.AllMet(), Is.False);
            Assert.That(trace, Is.EqualTo(new[] { "first", "failure" }));
        }

        [Test]
        public void AnyMetStopsAfterFirstSuccess()
        {
            var trace = new List<string>();
            ISFCondition[] conditions =
            {
                new SFPackageRecordingCondition("first", false, trace),
                new SFPackageRecordingCondition("success", true, trace),
                new SFPackageRecordingCondition("skipped", false, trace)
            };

            Assert.That(conditions.AnyMet(), Is.True);
            Assert.That(trace, Is.EqualTo(new[] { "first", "success" }));
        }

        [Test]
        public void NullCollectionIsTreatedAsUnrestrictedByBothAggregators()
        {
            ISFCondition[] conditions = null;

            Assert.That(conditions.AllMet(), Is.True);
            Assert.That(conditions.AnyMet(), Is.True);
        }

        [Test]
        public void EmptyCollectionPassesAllMetAndFailsAnyMet()
        {
            var conditions = new ISFCondition[0];

            Assert.That(conditions.AllMet(), Is.True);
            Assert.That(conditions.AnyMet(), Is.False);
        }

        [Test]
        public void EditorOverrideBypassesConditionEvaluation()
        {
            var trace = new List<string>();
            ISFCondition[] conditions = { new SFPackageRecordingCondition("must not run", false, trace) };

            Assert.That(conditions.AllMet(editorAlwaysTrue: true), Is.True);
            Assert.That(conditions.AnyMet(editorAlwaysTrue: true), Is.True);
            Assert.That(trace, Is.Empty);
        }

        [Test]
        public void ConverterRestoresCaseInsensitivePublicFieldsAndPropertiesAndIgnoresUnknownMembers()
        {
            const string json = "{\"$Type\":\"SFPackageThresholdCondition\",\"actual\":8,\"REQUIRED\":7,\"futureMember\":true}";

            var condition = JsonConvert.DeserializeObject<ISFCondition>(json);

            Assert.That(condition, Is.TypeOf<SFPackageThresholdCondition>());
            Assert.That(condition.Met(), Is.True);
            var threshold = (SFPackageThresholdCondition)condition;
            Assert.That(threshold.Actual, Is.EqualTo(8));
            Assert.That(threshold.Required, Is.EqualTo(7));
        }

        [Test]
        public void ConverterReadsNullCondition()
        {
            Assert.That(JsonConvert.DeserializeObject<ISFCondition>("null"), Is.Null);
        }

        [TestCase("{\"Value\":true}")]
        [TestCase("{\"$Type\":null}")]
        [TestCase("{\"$Type\":7}")]
        [TestCase("{\"$Type\":\"SFPackageUnknownCondition\",\"Value\":true}")]
        public void ConverterRejectsMissingOrUnknownTypeDiscriminator(string json)
        {
            Assert.That(() => JsonConvert.DeserializeObject<ISFCondition>(json),
                Throws.TypeOf<JsonSerializationException>());
        }

        [Test]
        public void ConverterRoundTripsNestedFieldConditionsArraysAndNullElements()
        {
            ISFCondition original = new SFPackageCompositeCondition
            {
                Main = new SFPackageBooleanCondition { Value = true },
                Optional = null,
                Others = new ISFCondition[] { new SFPackageBooleanCondition { Value = false }, null }
            };

            var json = JsonConvert.SerializeObject(original);
            var document = JObject.Parse(json);
            var restored = (SFPackageCompositeCondition)JsonConvert.DeserializeObject<ISFCondition>(json);

            Assert.That((string)document["$Type"], Is.EqualTo("SFPackageCompositeCondition"));
            Assert.That((string)document["Main"]["$Type"], Is.EqualTo("SFPackageBooleanCondition"));
            Assert.That(restored.Main.Met(), Is.True);
            Assert.That(restored.Optional, Is.Null);
            Assert.That(restored.Others, Has.Length.EqualTo(2));
            Assert.That(restored.Others[0].Met(), Is.False);
            Assert.That(restored.Others[1], Is.Null);
            Assert.That(restored.Met(), Is.False);
        }

        [Test]
        public void ConverterHandlesConditionTypesAndDeclinesUnrelatedObjects()
        {
            var converter = new SFConditionConverter();

            Assert.That(converter.CanConvert(typeof(ISFCondition)), Is.True);
            Assert.That(converter.CanConvert(typeof(SFPackageBooleanCondition)), Is.True);
            Assert.That(converter.CanConvert(typeof(string)), Is.False);
        }

        [Test]
        public void ConverterRoundTripsWritablePropertiesAndIgnoresNonDataMembers()
        {
            var original = new SFPackagePropertyCondition
            {
                Rule = new SFPackageThresholdCondition { Actual = 5, Required = 7 },
                Ignored = 42
            };

            var json = JObject.Parse(JsonConvert.SerializeObject(original));
            Assert.That((int)json["rule"]["Required"], Is.EqualTo(7));
            Assert.That(json["ReadOnly"], Is.Null);
            Assert.That(json["Item"], Is.Null);
            Assert.That(json["Ignored"], Is.Null);
            json["ReadOnly"] = 42;
            json["Item"] = 42;
            json["Ignored"] = 42;
            json["Met"] = 42;

            var restored = (SFPackagePropertyCondition)json.ToObject<ISFCondition>();
            Assert.That(restored.Met(), Is.False);
            Assert.That(restored.Ignored, Is.Zero);
        }

        [Test]
        public void ConverterRoundTripsDuplicateShortNamesAndRejectsAmbiguousLegacyIds()
        {
            var first = JObject.Parse(JsonConvert.SerializeObject(new Left.Duplicate()));
            var second = JObject.Parse(JsonConvert.SerializeObject(new Right.Duplicate()));
            Assert.That((string)first["$Type"], Is.EqualTo($"{typeof(Left.Duplicate).FullName}, {typeof(Left.Duplicate).Assembly.GetName().Name}"));
            Assert.That((string)second["$Type"], Is.EqualTo($"{typeof(Right.Duplicate).FullName}, {typeof(Right.Duplicate).Assembly.GetName().Name}"));
            Assert.That(first.ToObject<ISFCondition>(), Is.TypeOf<Left.Duplicate>());
            Assert.That(second.ToObject<ISFCondition>(), Is.TypeOf<Right.Duplicate>());
            Assert.That(() => JsonConvert.DeserializeObject<ISFCondition>("{\"$Type\":\"Duplicate\"}"),
                Throws.TypeOf<JsonSerializationException>().With.Message.Contains("Ambiguous"));
        }

        [Test]
        public void ConverterRejectsAConditionIncompatibleWithTheRequestedType()
        {
            Assert.That(() => JsonConvert.DeserializeObject<SFPackageThresholdCondition>("{\"$Type\":\"SFPackageBooleanCondition\"}"),
                Throws.TypeOf<JsonSerializationException>());
        }
    }

    public sealed class SFPackageBooleanCondition : SFCondition
    {
        public bool Value;
        public override bool Met() => Value;
    }

    public sealed class SFPackageThresholdCondition : SFCondition
    {
        public int Actual;
        public int Required { get; set; }
        public override bool Met() => Actual >= Required;
    }

    public sealed class SFPackageCompositeCondition : SFCondition
    {
        public ISFCondition Main;
        public ISFCondition Optional;
        public ISFCondition[] Others;
        public override bool Met() => Main.Met() && Others[0].Met();
    }

    public sealed class SFPackagePropertyCondition : SFCondition
    {
        [JsonProperty("rule")] public ISFCondition Rule { get; set; }
        [JsonIgnore] public int Ignored { get; set; }
        public int ReadOnly => 17;
        public int this[int index] { get => index; set { } }
        public override bool Met() => Rule.Met();
    }

    public sealed class SFPackageRecordingCondition : ISFCondition
    {
        private readonly string _name;
        private readonly bool _value;
        private readonly List<string> _trace;
        public SFPackageRecordingCondition(string name, bool value, List<string> trace)
        { _name = name; _value = value; _trace = trace; }
        public bool Met() { _trace.Add(_name); return _value; }
    }
}

namespace SFramework.Conditions.Tests.Left
{
    public sealed class Duplicate : ISFCondition { public bool Met() => true; }
}

namespace SFramework.Conditions.Tests.Right
{
    public sealed class Duplicate : ISFCondition { public bool Met() => false; }
}
