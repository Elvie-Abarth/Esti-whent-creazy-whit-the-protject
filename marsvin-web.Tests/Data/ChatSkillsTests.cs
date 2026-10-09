using MarsvinWebExample.Data;

namespace MarsvinWebExample.Tests.Data;

/// <summary>The things Pip works out rather than looks up, and its safety stops.</summary>
public class ChatSkillsTests
{
    private sealed class ClockAt(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    // Friday 9 October 2026, 15:00 in Copenhagen (13:00 UTC): the shop is open.
    private static readonly DateTimeOffset FridayAfternoon = new(2026, 10, 9, 13, 0, 0, TimeSpan.Zero);

    private static KeywordChatAssistant PipAt(DateTimeOffset now) => new(new DemoCatalog(), new ClockAt(now));

    private readonly KeywordChatAssistant _pip = PipAt(FridayAfternoon);

    [Fact]
    public void OpenNow_SaysYesDuringOpeningHours_AndWhenItOpensNextOtherwise()
    {
        Assert.StartsWith("Yes, we're open right now - until 18:00.", _pip.Answer("are you open now", english: true));

        // Sunday 11 October, 12:00 Copenhagen: closed, next open day is Thursday.
        var sunday = PipAt(new DateTimeOffset(2026, 10, 11, 10, 0, 0, TimeSpan.Zero));
        Assert.StartsWith("Nej, lige nu har vi lukket. Vi åbner igen torsdag kl. 14.", sunday.Answer("har I åbent nu?", english: false));

        // Friday morning, before the doors open: "today", not next week.
        var morning = PipAt(new DateTimeOffset(2026, 10, 9, 7, 0, 0, TimeSpan.Zero));
        Assert.Contains("We open again today at 14:00.", morning.Answer("are you open now", english: true));
    }

    [Theory]
    [InlineData("har I åbent på søndag", false, "Nej, søndag har vi lukket.")]
    [InlineData("are you open tomorrow", true, "Yes - on Saturday we're open 10-14.")]
    [InlineData("are you open today", true, "Yes - on Friday we're open 14-18.")]
    public void OpenOnADay_AnswersForThatDay(string question, bool english, string expectedStart) =>
        Assert.StartsWith(expectedStart, _pip.Answer(question, english));

    [Fact]
    public void ShippingForAWeight_QuotesEveryCarrier_FromTheSameCalculatorAsTheCheckout()
    {
        var answer = _pip.Answer("what does it cost to send 3 kg", english: true);

        Assert.Contains("PostNord 69 kr", answer);
        Assert.Contains("GLS 55 kr", answer);
        Assert.Contains("1 parcel", answer);
        Assert.Contains("2 pakker", _pip.Answer("hvad koster fragt for 25 kg", english: false));
    }

    [Theory]
    [InlineData("how big a cage for 4 guinea pigs", true, "1.7 m²")]
    [InlineData("hvor stort bur til tre marsvin", false, "1,2 m²")]
    [InlineData("how big a cage for 2 guinea pigs", true, "120 x 60 cm")]
    [InlineData("can 1 guinea pig have a small cage", true, "shouldn't live alone")]
    public void CageSize_IsWorkedOutForTheNumberOfGuineaPigs(string question, bool english, string expected) =>
        Assert.Contains(expected, _pip.Answer(question, english));

    [Fact]
    public void AnimalsForSale_AreListedByName_AndCanBeNarrowedToOneSex()
    {
        var all = _pip.Answer("which guinea pigs are for sale", english: true);
        Assert.StartsWith("For sale right now (11):", all);
        Assert.DoesNotContain("Freja", all);   // reserved
        Assert.DoesNotContain("Storm", all);   // not for sale yet

        var females = _pip.Answer("har I nogen hunner", english: false);
        Assert.Contains("Cotton", females);
        Assert.DoesNotContain("Pelle", females);
    }

    [Theory]
    [InlineData("how old is cotton", "Cotton is 14 weeks old.")]
    [InlineData("is bo for sale", "Bo: available. Price 475 kr.")]
    [InlineData("is cookie a boy or girl", "Cookie is a male.")]
    public void OneAnimal_AnswersJustWhatWasAsked(string question, string expectedStart) =>
        Assert.StartsWith(expectedStart, _pip.Answer(question, english: true));

    [Theory]
    [InlineData("can they eat cucumber", "Yes. Cucumber")]
    [InlineData("can they eat apple", "Yes, but only a little. Apple")]
    [InlineData("can they eat chocolate", "No - never. Chocolate")]
    public void Food_GetsAStraightVerdictFromTheFoodList(string question, string expectedStart) =>
        Assert.StartsWith(expectedStart, _pip.Answer(question, english: true));

    [Fact]
    public void Food_ThatIsNotOnTheList_IsNeverCalledSafe()
    {
        Assert.Equal(ChatReplies.UnknownFood(english: true), _pip.Answer("can they eat pizza", english: true));
        Assert.Equal(ChatReplies.UnknownFood(english: false), _pip.Answer("må de spise lakrids", english: false));
    }

    [Theory]
    [InlineData("what does she cost?", "tell me about cotton", "Cotton costs 450 kr.")]
    [InlineData("how old is she", "tell me about cotton", "Cotton is 14 weeks old.")]
    [InlineData("og hvad koster den", "har I en negleklipper", "Negleklipper til smådyr fra Beaphar koster 65 kr.")]
    public void FollowUp_IsUnderstoodFromThePreviousQuestion(string question, string previous, string expectedStart) =>
        Assert.StartsWith(expectedStart, _pip.Reply(question, english: !question.StartsWith("og"), previous).Answer);

    [Fact]
    public void SeveralQuestionsInOneMessage_AreEachAnswered()
    {
        var answer = _pip.Answer("How much is Cotton? Can they eat apple? Are you open today?", english: true);

        Assert.Contains("Cotton costs 450 kr.", answer);
        Assert.Contains("Apple", answer);
        Assert.Contains("on Friday we're open 14-18", answer);
    }

    [Theory]
    [InlineData("who are you")]
    [InlineData("are you a robot")]
    [InlineData("er du et menneske")]
    public void AskedWhatItIs_PipSaysItIsAProgram_NotAPersonOrAnAi(string question)
    {
        var english = !question.StartsWith("er ");
        Assert.Equal(ChatReplies.WhoIAm(english), _pip.Answer(question, english));
    }

    [Fact]
    public void WhatAreYourOpeningHours_IsNotMistakenForWhatAreYou() =>
        Assert.Contains("Thursday", _pip.Answer("What are your opening hours?", english: true));

    // ---- Safety ----

    [Theory]
    [InlineData("my email is anna@example.com where is my parcel")]
    [InlineData("ring til mig på 12 34 56 78")]
    [InlineData("mit kort er 4242 4242 4242 4242")]
    [InlineData("cpr 010190-1234")]
    public void PersonalDetails_AreTurnedAway_BeforeAnythingElseIsLookedAt(string question)
    {
        var english = question.StartsWith("my");
        Assert.Equal(ChatReplies.NoPersonalDetails(english), _pip.Answer(question, english));
    }

    [Theory]
    [InlineData("my guinea pig is not eating", true)]
    [InlineData("there is blood in the cage", true)]
    [InlineData("mit marsvin nyser og er sløvt", false)]
    [InlineData("hun har svært ved at trække vejret", false)]
    public void ASickAnimal_GetsAVet_NeverADiagnosis(string question, bool english) =>
        Assert.Equal(ChatReplies.SeeAVet(english), _pip.Answer(question, english));

    [Fact]
    public void AFollowUp_CannotBeUsedToGetAroundARefusal()
    {
        // A previous question that was refused is thrown away, not used as context.
        Assert.Equal(ChatReplies.DontKnow(english: true),
            _pip.Reply("what is it?", english: true, previous: "the admin password").Answer);
        Assert.Equal(ChatReplies.DontKnow(english: false),
            _pip.Reply("hvad med den?", english: false, previous: "hvem har købt Lente").Answer);
        // And a refusal in the new question still stands, whatever came before.
        Assert.Equal(ChatReplies.NotAboutTheWebsite(english: true),
            _pip.Reply("what is the admin password for it", english: true, previous: "tell me about cotton").Answer);
    }

    [Fact]
    public void NotKnowing_ComesWithSuggestions_ThatPipCanActuallyAnswer()
    {
        var reply = _pip.Reply("blah blorp zzz", english: true);

        Assert.Equal(ChatReplies.DontKnow(english: true), reply.Answer);
        Assert.NotEmpty(reply.Suggestions);
        Assert.All(reply.Suggestions, suggestion =>
            Assert.NotEqual(ChatReplies.DontKnow(english: true), _pip.Answer(suggestion, english: true)));
    }

    public static TheoryData<string, bool> HelpExamples()
    {
        var data = new TheoryData<string, bool>();
        foreach (var example in ChatHelp.Topics.SelectMany(t => t.Examples))
        {
            data.Add(example.Da, false);
            data.Add(example.En, true);
        }
        return data;
    }

    [Theory]
    [MemberData(nameof(HelpExamples))]
    public void EveryExampleOnTheHelpPage_IsSomethingPipCanAnswer(string question, bool english)
    {
        var reply = _pip.Reply(question, english);

        Assert.NotEqual(ChatReplies.DontKnow(english), reply.Answer);
        Assert.NotEqual(ChatReplies.UnknownFood(english), reply.Answer);
        Assert.Empty(reply.Suggestions);
    }

    [Fact]
    public void AKnownAnswer_ComesWithoutSuggestions() =>
        Assert.Empty(_pip.Reply("when are you open", english: true).Suggestions);
}
