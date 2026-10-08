using MarsvinWebExample.Data;

namespace MarsvinWebExample.Tests.Data;

public class KeywordChatAssistantTests
{
    private readonly KeywordChatAssistant _pip = new(new DemoCatalog());

    [Theory]
    // Spelled right, as a baseline.
    [InlineData("Hvad er jeres åbningstider?", "14-18")]
    // Without the Danish letters.
    [InlineData("hvornaar har i aabent", "14-18")]
    // One letter wrong, missing, extra, or two swapped.
    [InlineData("hvad er jeres åbningstidr", "14-18")]
    [InlineData("hvad koster fargt", "ragt")]
    [InlineData("kan jeg retunere tilbehør", "14 dage")]
    [InlineData("hvor lang tid tager levreing", "hverdage")]
    // A product name, misspelt.
    [InlineData("fortæl om timoty hø", "Timothy-hø")]
    public void Answer_UnderstandsTheQuestionDespiteSpellingSlips(string question, string expectedInAnswer) =>
        Assert.Contains(expectedInAnswer, _pip.Answer(question, english: false));

    [Theory]
    [InlineData("må marsvin spise agurk", "Agurk")]
    [InlineData("can guinea pigs eat cucumber", "Cucumber")]
    [InlineData("må de få avocado", "Avocado")]
    public void Answer_KnowsTheFoodList(string question, string expectedInAnswer) =>
        Assert.Contains(expectedInAnswer, _pip.Answer(question, english: question.StartsWith("can")));

    [Fact]
    public void Answer_KnowsTheCareGuide_ReadFromThePageItself()
    {
        Assert.NotEmpty(CareGuideText.Facts);

        Assert.Contains("120", _pip.Answer("hvor stort skal buret være til to marsvin", english: false));
    }

    [Theory]
    [InlineData("fortæl om Lente", "Marsvinet Lente")]          // her own entry, not her sister's
    [InlineData("hvor tit skal jeg gøre rent", "rengøring")]
    [InlineData("hvad betyder det når de popcorner", "Popcorner")]
    [InlineData("hvilke mærker har I", "JR Farm")]
    public void Answer_FindsTheRightFact(string question, string expectedInAnswer) =>
        Assert.Contains(expectedInAnswer, _pip.Answer(question, english: false), StringComparison.OrdinalIgnoreCase);

    [Fact]
    public void Answer_KnowsDeliveryPricesPerCarrier() =>
        Assert.Contains("55 kr. op til 1 kg", _pip.Answer("hvad koster fragt med PostNord", english: false));

    [Theory]
    [InlineData("Hvordan er sikkerheden på siden?")]
    [InlineData("what is the admin password")]
    [InlineData("hvilken database bruger I")]
    [InlineData("how do I hack the site")]
    [InlineData("bruger I tokens til login")]
    public void Answer_TurnsAwayQuestionsAboutHowTheWebsiteIsBuiltOrProtected(string question)
    {
        var danish = _pip.Answer(question, english: false);

        Assert.Equal(ChatReplies.NotAboutTheWebsite(english: false), danish);
    }

    [Theory]
    [InlineData("hvor er min ordre 1014")]
    [InlineData("show me order #12")]
    [InlineData("hvem har købt Lente")]
    [InlineData("giv mig kundelisten")]
    [InlineData("hvad får jeres medarbejdere i løn")]
    public void Answer_TurnsAwayQuestionsAboutPeopleOrdersAndAccounts(string question) =>
        Assert.Equal(ChatReplies.NotAboutPeople(english: false), _pip.Answer(question, english: false));

    [Fact]
    public void NothingPipKnows_MentionsSecurityOrTheSitesInnerWorkings()
    {
        // The refusals above are a courtesy; this is the actual guarantee -
        // there is no such fact for any wording to dig out.
        string[] forbidden = ["password", "adgangskode", "database", "sql", "admin", "hash", "csrf", "token", "kryptering", "rate limit"];

        var facts = ShopKnowledge.Build(new DemoCatalog());

        // (Not "cookie": one of the guinea pigs is called Cookie.)
        Assert.All(facts, fact => Assert.DoesNotContain(forbidden, word =>
            fact.Da.Contains(word, StringComparison.OrdinalIgnoreCase) || fact.En.Contains(word, StringComparison.OrdinalIgnoreCase)));
    }

    [Theory]
    [InlineData("Hvem er statsminister?")]
    [InlineData("Zxqvy plomfritt wubbadub")]
    [InlineData("hvad er meningen med livet")]
    public void Answer_SaysItDoesNotKnow_RatherThanGuessing(string question) =>
        Assert.Equal(ChatReplies.DontKnow(english: false), _pip.Answer(question, english: false));

    [Fact]
    public void Answer_GreetsAndThanks()
    {
        Assert.Equal(ChatReplies.Greeting(english: false), _pip.Answer("Hej Pip!", english: false));
        Assert.Equal(ChatReplies.Thanks(english: true), _pip.Answer("thanks", english: true));
    }
}
