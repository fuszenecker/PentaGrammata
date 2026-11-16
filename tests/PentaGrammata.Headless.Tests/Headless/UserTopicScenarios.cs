using Avalonia.Controls;
using Avalonia.LogicalTree;
using Microsoft.Extensions.DependencyInjection;
using PentaGrammata.Configuration;
using PentaGrammata.Interfaces;
using PentaGrammata.Models;
using PentaGrammata.Services;
using PentaGrammata.ViewModels;
using PentaGrammata.Views;

namespace PentaGrammata.Tests.Headless;

// The practice topics: the topic dropdown of the main window, the Topics tab of the settings
// dialog, and what each topic actually sends.
public sealed partial class UserJourneyScenarios
{
    private static void ArrangeTopicLists(AppConfiguration c)
    {
        c.Topics = new Topics
        {
            CallsignPatternsEmea = ["HA#xyz"],
            CallsignPatternsGlobal = ["JA1xy"],
            Cities = ["WIEN", "CAPE TOWN"],
            Names = ["LACI"],
            Abbreviations = ["QRZ", "73"],
        };
    }

    private static void SelectTopic(ScenarioDesktop app, PracticeTopic topic) =>
        app.TopicBox.SelectedItem = app.TopicBox.Items.OfType<PracticeTopicOption>().Single(o => o.Topic == topic);

    [TestMethod]
    [DataRow(PracticeTopic.CallsignsEmea)]
    [DataRow(PracticeTopic.CallsignsGlobal)]
    [DataRow(PracticeTopic.Cities)]
    [DataRow(PracticeTopic.Names)]
    [DataRow(PracticeTopic.Abbreviations)]
    public Task UserPicksATopicAndCopiesItemsFromItsList(PracticeTopic topic) => Run(async app =>
    {
        SelectTopic(app, topic);
        Assert.IsFalse(app.CharacterSetBox.IsEffectivelyEnabled, "Only group topics use a character set.");
        Assert.IsTrue(ScenarioDesktop.Controls<NumericUpDown>(app.Main).Single().IsEffectivelyEnabled, "The duration decides how much is sent.");

        await app.Practice("@sent");

        var sent = app.Services.GetRequiredService<IPracticeController>().LastGeneratedText;
        bool Matches(string item) => topic switch
        {
            PracticeTopic.CallsignsEmea => item.Length == 6 && item.StartsWith("HA", StringComparison.Ordinal) && char.IsDigit(item[2]) && item[3..].All(char.IsAsciiLetterUpper),
            PracticeTopic.CallsignsGlobal => item.Length == 5 && item.StartsWith("JA1", StringComparison.Ordinal) && item[3..].All(char.IsAsciiLetterUpper),
            PracticeTopic.Cities => item is "WIEN" or "CAPE_TOWN",
            PracticeTopic.Names => item == "LACI",
            _ => item is "QRZ" or "73",
        };
        Assert.IsTrue(PracticeItems.SplitItems(sent).All(Matches), sent);
        // 1 minute at 15 WPM is worth 10 groups of 5 characters.
        Assert.IsGreaterThanOrEqualTo(50, string.Concat(PracticeItems.SplitWords(sent)).Length, sent);

        var result = await app.Result();
        Assert.AreEqual("0", ((PracticeResultWindowViewModel)result.DataContext!).ErrorsText);
        await app.SaveResult(result);
        await app.CloseResult(result);
        Assert.AreEqual(topic, app.Configuration.Current.Practice.Topic);
    }, ArrangeTopicLists);

    [TestMethod]
    public Task UserMixesEveryTopicInOneSession() => Run(async app =>
    {
        SelectTopic(app, PracticeTopic.Mixed);
        Assert.IsTrue(app.CharacterSetBox.IsEffectivelyEnabled, "Mixed sends groups from the character set too.");
        await app.Practice("@sent");

        var items = app.Services.GetRequiredService<IPracticeController>().LastGeneratedText.Split(' ');
        Assert.IsTrue(items.All(i => i == "AAAAA" || i.StartsWith("HA", StringComparison.Ordinal) || i.StartsWith("JA1", StringComparison.Ordinal)
            || i is "WIEN" or "CAPE_TOWN" or "LACI" or "QRZ" or "73" or "TEST"), string.Join(' ', items));
        Assert.IsGreaterThan(1, items.Select(i => i[..Math.Min(2, i.Length)]).Distinct().Count(), "A mixed session draws from more than one topic.");
        var result = await app.Result();
        Assert.AreEqual("0", ((PracticeResultWindowViewModel)result.DataContext!).ErrorsText);
        await app.CloseResult(result);
    }, c =>
    {
        ArrangeTopicLists(c);
        c.Practice.DefaultDurationMins = 5;
    });

    [TestMethod]
    public Task UserSendsCustomTextWithoutDurationOrCharacterSet() => Run(async app =>
    {
        Assert.AreEqual(PracticeTopic.CustomText, ((PracticeTopicOption)app.TopicBox.SelectedItem!).Topic);
        Assert.IsFalse(app.CharacterSetBox.IsEffectivelyEnabled);
        Assert.IsFalse(ScenarioDesktop.Controls<NumericUpDown>(app.Main).Single().IsEffectivelyEnabled);
        SelectTopic(app, PracticeTopic.Groups);
        Assert.IsTrue(app.CharacterSetBox.IsEffectivelyEnabled);
        Assert.IsTrue(ScenarioDesktop.Controls<NumericUpDown>(app.Main).Single().IsEffectivelyEnabled);
        SelectTopic(app, PracticeTopic.CustomText);

        await app.Practice("@sent");
        Assert.AreEqual("TEST", app.Services.GetRequiredService<IPracticeController>().LastGeneratedText);
        var result = await app.Result();
        await app.CloseResult(result);
    });

    [TestMethod]
    public Task UserShufflesTheWordsOfTheCustomText() => Run(async app =>
    {
        var settings = await Settings(app);
        var box = ScenarioDesktop.CustomText(settings);
        var shuffle = ScenarioDesktop.Tip<CheckBox>(settings, "Send the words of the custom text in a random order");
        ScenarioDesktop.Text(settings, box, "ONE TWO THREE FOUR FIVE SIX SEVEN EIGHT NINE TEN");
        ScenarioDesktop.Click(settings, shuffle);
        Assert.IsTrue(shuffle.IsChecked);
        await ApplySettings(app, settings);
        Assert.IsTrue(app.Configuration.Current.Practice.ShuffleCustomText);

        var orders = new List<string>();
        for (int i = 0; i < 4; i++)
        {
            await app.Practice(null, stop: true);
            orders.Add(app.Services.GetRequiredService<IPracticeController>().LastGeneratedText);
        }

        foreach (var order in orders)
        {
            CollectionAssert.AreEquivalent("ONE TWO THREE FOUR FIVE SIX SEVEN EIGHT NINE TEN".Split(' '), order.Split(' '));
        }

        Assert.IsGreaterThan(1, orders.Distinct().Count(), "Four shuffled sessions all sent the same order.");
    });

    [TestMethod]
    public Task UserFindsTopicsBetweenPracticeAndCharacterSets() => Run(async app =>
    {
        var settings = await Settings(app);
        var outer = ScenarioDesktop.Controls<TabControl>(settings).Single(t => t.FindLogicalAncestorOfType<TabControl>() is null);
        CollectionAssert.AreEqual(new[] { "Practice", "Topics", "Character sets", "Audio", "Noise" },
            outer.Items.OfType<TabItem>().Select(t => t.Header?.ToString()).ToArray());
        ScenarioDesktop.Tab(settings, ScenarioDesktop.TopicsTab);
        var inner = ScenarioDesktop.Controls<TabControl>(settings).Single(t => t.FindLogicalAncestorOfType<TabControl>() is not null);
        CollectionAssert.AreEqual(new[] { "Callsigns (EMEA)", "Callsigns (global)", "Cities", "Names", "Abbreviations", "Custom text" },
            inner.Items.OfType<TabItem>().Select(t => t.Header?.ToString()).ToArray());
        ScenarioDesktop.Click(settings, "Cancel");
    });

    [TestMethod]
    public Task UserEditsATopicListAndItIsSavedInAlphabeticalOrder() => Run(async app =>
    {
        var settings = await Settings(app);
        ScenarioDesktop.Tab(settings, ScenarioDesktop.TopicsTab);
        ScenarioDesktop.SubTab(settings, ScenarioDesktop.CitiesSubTab);
        var cities = ScenarioDesktop.Tip<TextBox>(settings, "City names");
        Assert.AreEqual("CAPE TOWN" + Environment.NewLine + "WIEN", cities.Text);
        ScenarioDesktop.Text(settings, cities, "ROMA\nBUDAPEST\n\nroma");
        await ApplySettings(app, settings);

        CollectionAssert.AreEqual(new[] { "BUDAPEST", "ROMA" }, app.Configuration.Current.Topics.Cities);
        await app.Configuration.FlushAsync();
        var reloaded = app.Services.GetRequiredService<IConfigurationStore>().Load();
        CollectionAssert.AreEqual(new[] { "BUDAPEST", "ROMA" }, reloaded.Topics.Cities);
        CollectionAssert.AreEqual(new[] { "LACI" }, reloaded.Topics.Names);

        settings = await Settings(app);
        ScenarioDesktop.Tab(settings, ScenarioDesktop.TopicsTab);
        ScenarioDesktop.SubTab(settings, ScenarioDesktop.CitiesSubTab);
        Assert.AreEqual("BUDAPEST" + Environment.NewLine + "ROMA", ScenarioDesktop.Tip<TextBox>(settings, "City names").Text);
        ScenarioDesktop.Click(settings, "Cancel");
    }, ArrangeTopicLists);

    [TestMethod]
    [DataRow(ScenarioDesktop.CitiesSubTab, "City names", "MÜNCHEN")]
    [DataRow(ScenarioDesktop.EmeaCallsignsSubTab, "EMEA callsign patterns", "HA# xyz")]
    [DataRow(ScenarioDesktop.GlobalCallsignsSubTab, "Worldwide callsign patterns", "W#x$")]
    public Task UserCannotSaveAnUnsendableTopicEntryAndSeesWhy(int subTab, string tip, string entry) => Run(async app =>
    {
        var settings = await Settings(app);
        ScenarioDesktop.Tab(settings, ScenarioDesktop.TopicsTab);
        ScenarioDesktop.SubTab(settings, subTab);
        ScenarioDesktop.Text(settings, ScenarioDesktop.Tip<TextBox>(settings, tip), entry);
        ScenarioDesktop.Click(settings, "Save");

        Assert.IsTrue(settings.IsVisible);
        var error = ((MorseSettingsDialogViewModel)settings.DataContext!).ErrorMessage;
        Assert.Contains(entry, error);
        Assert.IsTrue(ScenarioDesktop.Controls<TextBlock>(settings).Any(t => t.Text == error && t.IsVisible));
        ScenarioDesktop.Click(settings, "Cancel");
        CollectionAssert.AreEqual(new[] { "WIEN", "CAPE TOWN" }, app.Configuration.Current.Topics.Cities);
    }, ArrangeTopicLists);

    [TestMethod]
    public Task UserCannotEmptyTheCustomTextWhileItIsTheTopic() => Run(async app =>
    {
        var settings = await Settings(app);
        ScenarioDesktop.Text(settings, ScenarioDesktop.CustomText(settings), " ");
        ScenarioDesktop.Click(settings, "Save");

        Assert.IsTrue(settings.IsVisible);
        Assert.AreEqual("The custom text topic needs a custom text.", ((MorseSettingsDialogViewModel)settings.DataContext!).ErrorMessage);
        ScenarioDesktop.Click(settings, "Cancel");
        Assert.AreEqual("TEST", app.Configuration.Current.Practice.CustomText);
    });

    [TestMethod]
    public Task UserTopicChoiceSurvivesARestart() => Run(async app =>
    {
        SelectTopic(app, PracticeTopic.Names);
        await app.Configuration.FlushAsync();

        var reloaded = app.Services.GetRequiredService<IConfigurationStore>().Load();
        Assert.AreEqual(PracticeTopic.Names, reloaded.Practice.Topic);
    }, ArrangeTopicLists);

    [TestMethod]
    [DataRow(ScenarioDesktop.EmeaCallsignsSubTab, "EMEA callsign patterns", PracticeTopic.CallsignsEmea, "OK#xyz\nDL#xy\nok#xyz", "DL#xy|OK#xyz")]
    [DataRow(ScenarioDesktop.GlobalCallsignsSubTab, "Worldwide callsign patterns", PracticeTopic.CallsignsGlobal, "VK#xyz\n  W#xy  \n\nW#XY", "VK#xyz|W#xy")]
    [DataRow(ScenarioDesktop.NamesSubTab, "Names, one per line", PracticeTopic.Names, "PISTA\nANNA\npista", "ANNA|PISTA")]
    [DataRow(ScenarioDesktop.AbbreviationsSubTab, "Radio amateur abbreviations", PracticeTopic.Abbreviations, "QSL\n73\nCQ", "73|CQ|QSL")]
    public Task UserEditsEachTopicListAndHearsTheNewEntries(int subTab, string tip, PracticeTopic topic, string typed, string expected) => Run(async app =>
    {
        var settings = await Settings(app);
        ScenarioDesktop.Tab(settings, ScenarioDesktop.TopicsTab);
        ScenarioDesktop.SubTab(settings, subTab);
        ScenarioDesktop.Text(settings, ScenarioDesktop.Tip<TextBox>(settings, tip), typed);
        await ApplySettings(app, settings);

        var list = topic switch
        {
            PracticeTopic.CallsignsEmea => app.Configuration.Current.Topics.CallsignPatternsEmea,
            PracticeTopic.CallsignsGlobal => app.Configuration.Current.Topics.CallsignPatternsGlobal,
            PracticeTopic.Names => app.Configuration.Current.Topics.Names,
            _ => app.Configuration.Current.Topics.Abbreviations,
        };
        CollectionAssert.AreEqual(expected.Split('|'), list, "Saved sorted, trimmed, without repeats.");
        CollectionAssert.AreEqual(new[] { "CAPE TOWN", "WIEN" }, app.Configuration.Current.Topics.Cities, "The other lists keep their entries (in alphabetical order).");

        SelectTopic(app, topic);
        await app.Practice("@sent");
        var sent = app.Services.GetRequiredService<IPracticeController>().LastGeneratedText.Split(' ');
        bool Matches(string item) => topic switch
        {
            PracticeTopic.CallsignsEmea => (item.StartsWith("OK", StringComparison.Ordinal) && item.Length == 6) || (item.StartsWith("DL", StringComparison.Ordinal) && item.Length == 5),
            PracticeTopic.CallsignsGlobal => (item.StartsWith("VK", StringComparison.Ordinal) && item.Length == 6) || (item.StartsWith('W') && item.Length == 4),
            PracticeTopic.Names => item is "ANNA" or "PISTA",
            _ => item is "QSL" or "73" or "CQ",
        };
        Assert.IsTrue(sent.All(Matches), string.Join(' ', sent));
        var result = await app.Result();
        Assert.AreEqual("0", ((PracticeResultWindowViewModel)result.DataContext!).ErrorsText);
        await app.CloseResult(result);
    }, ArrangeTopicLists);

    [TestMethod]
    [DataRow(PracticeTopic.Cities, ScenarioDesktop.CitiesSubTab, "City names", "cities")]
    [DataRow(PracticeTopic.Names, ScenarioDesktop.NamesSubTab, "Names, one per line", "names")]
    [DataRow(PracticeTopic.CallsignsEmea, ScenarioDesktop.EmeaCallsignsSubTab, "EMEA callsign patterns", "EMEA callsign patterns")]
    public Task UserCannotEmptyTheListOfTheSelectedTopic(PracticeTopic topic, int subTab, string tip, string listName) => Run(async app =>
    {
        SelectTopic(app, topic);
        var settings = await Settings(app);
        ScenarioDesktop.Tab(settings, ScenarioDesktop.TopicsTab);
        ScenarioDesktop.SubTab(settings, subTab);
        ScenarioDesktop.Text(settings, ScenarioDesktop.Tip<TextBox>(settings, tip), " ");
        ScenarioDesktop.Click(settings, "Save");

        Assert.IsTrue(settings.IsVisible);
        Assert.AreEqual($"The selected topic needs at least one entry in the list of {listName}.", ((MorseSettingsDialogViewModel)settings.DataContext!).ErrorMessage);
        ScenarioDesktop.Click(settings, "Cancel");

        // Another topic may leave that list empty.
        SelectTopic(app, PracticeTopic.Groups);
        settings = await Settings(app);
        ScenarioDesktop.Tab(settings, ScenarioDesktop.TopicsTab);
        ScenarioDesktop.SubTab(settings, subTab);
        ScenarioDesktop.Text(settings, ScenarioDesktop.Tip<TextBox>(settings, tip), " ");
        await ApplySettings(app, settings);
    }, ArrangeTopicLists);

    [TestMethod]
    public Task UserBacksUpTopicsAndGetsThemBackOnImport() => Run(async app =>
    {
        SelectTopic(app, PracticeTopic.Cities);
        var archivePath = Path.Combine(app.DirectoryPath, "topics.prmt");
        app.SaveChoices.Enqueue(archivePath);
        app.Menu("_Export");
        await ScenarioDesktop.Until(() => !app.ViewModel.ExportCommand.IsRunning, "export backup");

        var settings = await Settings(app);
        ScenarioDesktop.Tab(settings, ScenarioDesktop.TopicsTab);
        ScenarioDesktop.SubTab(settings, ScenarioDesktop.CitiesSubTab);
        ScenarioDesktop.Text(settings, ScenarioDesktop.Tip<TextBox>(settings, "City names"), "ROMA");
        ScenarioDesktop.SubTab(settings, ScenarioDesktop.CustomTextSubTab);
        ScenarioDesktop.Click(settings, ScenarioDesktop.Tip<CheckBox>(settings, "Send the words of the custom text in a random order"));
        await ApplySettings(app, settings);
        SelectTopic(app, PracticeTopic.Names);
        CollectionAssert.AreEqual(new[] { "ROMA" }, app.Configuration.Current.Topics.Cities);

        app.OpenChoices.Enqueue(archivePath);
        app.Menu("_Import");
        ScenarioDesktop.Click(await app.Dialog<ConfirmDialog>(), "Replace");
        var info = await app.Dialog<InfoDialog>();
        Assert.AreEqual("Import complete", info.Title);
        ScenarioDesktop.Click(info, "OK");
        await ScenarioDesktop.Until(() => !app.ViewModel.ImportCommand.IsRunning, "finish backup import");

        CollectionAssert.AreEqual(new[] { "WIEN", "CAPE TOWN" }, app.Configuration.Current.Topics.Cities);
        CollectionAssert.AreEqual(new[] { "HA#xyz" }, app.Configuration.Current.Topics.CallsignPatternsEmea);
        Assert.IsFalse(app.Configuration.Current.Practice.ShuffleCustomText);
        Assert.AreEqual(PracticeTopic.Cities, app.Configuration.Current.Practice.Topic);
        Assert.AreEqual(PracticeTopic.Cities, ((PracticeTopicOption)app.TopicBox.SelectedItem!).Topic, "The main window shows the imported topic.");
        Assert.IsFalse(app.CharacterSetBox.IsEffectivelyEnabled);
    }, ArrangeTopicLists);

    [TestMethod]
    public Task UserPracticesTheirConfusionsFromAnotherTopicAndGetsGroups() => Run(async app =>
    {
        await ScoreAndSave(app, "TEXT");
        SelectTopic(app, PracticeTopic.Cities);
        Assert.IsFalse(app.CharacterSetBox.IsEffectivelyEnabled);

        app.Menu("_Confusions");
        var confusions = await app.Dialog<ConfusionsDialog>();
        ScenarioDesktop.Click(confusions, "Practice confusions");
        await ScenarioDesktop.Until(() => !app.ViewModel.OpenConfusionsCommand.IsRunning, "select confusion practice set");

        Assert.AreEqual(PracticeTopic.Groups, ((PracticeTopicOption)app.TopicBox.SelectedItem!).Topic);
        Assert.AreEqual("Practice confusions", app.CharacterSetBox.SelectedItem);
        Assert.IsTrue(app.CharacterSetBox.IsEffectivelyEnabled);
        var palette = app.Configuration.Current.CharacterSets["Practice confusions"];
        await app.Practice("@sent");
        var groups = app.Services.GetRequiredService<IPracticeController>().LastGeneratedText.Split(' ');
        Assert.IsTrue(groups.All(g => g.Length == 5 && g.All(c => palette.Contains(c))), string.Join(' ', groups));
        var result = await app.Result();
        await app.CloseResult(result);
    }, ArrangeTopicLists);

    [TestMethod]
    public Task UserMixesTopicsWithAnotherCharacterSet() => Run(async app =>
    {
        SelectTopic(app, PracticeTopic.Mixed);
        app.CharacterSetBox.SelectedItem = "Digits";
        await app.Practice("@sent");

        var items = app.Services.GetRequiredService<IPracticeController>().LastGeneratedText.Split(' ');
        var groups = items.Where(i => i.Length == 5 && i.All(char.IsDigit)).ToList();
        Assert.IsNotEmpty(groups, string.Join(' ', items));
        Assert.DoesNotContain("AAAAA", items, "Groups come from the newly selected character set.");
        Assert.AreEqual("Digits", app.Configuration.Current.Practice.DefaultCharacterSet);
        var result = await app.Result();
        await app.CloseResult(result);
    }, c =>
    {
        ArrangeTopicLists(c);
        c.Practice.DefaultDurationMins = 5;
    });

    [TestMethod]
    public Task UserKeepsTheCustomTextInOrderWithoutShuffle() => Run(async app =>
    {
        var settings = await Settings(app);
        var box = ScenarioDesktop.CustomText(settings);
        Assert.IsFalse(ScenarioDesktop.Tip<CheckBox>(settings, "Send the words of the custom text in a random order").IsChecked);
        ScenarioDesktop.Text(settings, box, "ONE TWO THREE FOUR FIVE SIX");
        await ApplySettings(app, settings);

        for (int i = 0; i < 3; i++)
        {
            await app.Practice(null, stop: true);
            Assert.AreEqual("ONE TWO THREE FOUR FIVE SIX", app.Services.GetRequiredService<IPracticeController>().LastGeneratedText);
        }
    });

    [TestMethod]
    public Task UserCopiesMultiWordCityNamesAndSeesEachCityInOneRow() => Run(async app =>
    {
        SelectTopic(app, PracticeTopic.Cities);
        await app.Practice(null);
        var sent = app.Services.GetRequiredService<IPracticeController>().LastGeneratedText;
        var items = PracticeItems.SplitItems(sent);
        Assert.IsTrue(items.All(i => i == "CAPE_TOWN"), sent);

        // Typed with ordinary spaces, one mistake in the first city.
        var plain = PracticeItems.ToPlain(sent);
        var index = plain.IndexOf("TOWN", StringComparison.Ordinal);
        ScenarioDesktop.Text(app.Main, app.Main.FindControl<TextBox>("ReceivedTextBox")!, plain[..index] + "TOWM" + plain[(index + 4)..]);
        var result = await app.Result();
        var vm = (PracticeResultWindowViewModel)result.DataContext!;

        Assert.AreEqual("1", vm.ErrorsText);
        Assert.HasCount(items.Count, vm.Rows, "One row per city, not per word.");
        // Shown with a space that does not break the city across lines, never with "_".
        Assert.AreEqual("CAPE\u00A0TOWM", vm.Rows[0].ReceivedGroup);
        Assert.IsTrue(vm.Rows.All(r => r.SentGroup == "CAPE\u00A0TOWN"));
        Assert.IsTrue(vm.Rows.Skip(1).All(r => r.ReceivedGroup == "CAPE\u00A0TOWN"));
        Assert.IsTrue(ScenarioDesktop.Controls<TextBlock>(result).Any(t => t.Text == "CAPE\u00A0TOWN"), "The city is on screen in one piece.");
        Assert.IsFalse(ScenarioDesktop.Controls<TextBlock>(result).Any(t => t.Text?.Contains('_') == true), "No underscore is ever shown.");
        Assert.AreEqual(((PracticeResultWindowViewModel)result.DataContext!).Rows.Count, items.Count);
        await app.SaveResult(result);
        await app.CloseResult(result);
        var saved = (await app.Services.GetRequiredService<IPracticeResultStatisticsService>().GetStatisticsRecordsAsync()).Single();
        Assert.AreEqual(items.Count * 8, saved.CharacterCount, "The space inside a city is not a character.");
        Assert.AreEqual(1, saved.ErrorCount);
    }, c =>
    {
        ArrangeTopicLists(c);
        c.Topics.Cities = ["CAPE TOWN"];
    });

    [TestMethod]
    [DataRow(PracticeTopic.Names, "LITTLE JOHN")]
    [DataRow(PracticeTopic.Abbreviations, "QSL VIA BURO")]
    public Task UserSeesTheRevealedTextWithOrdinarySpacesAndCopiesMultiWordItems(PracticeTopic topic, string entry) => Run(async app =>
    {
        SelectTopic(app, topic);
        await app.Practice(null);

        var revealed = app.ViewModel.Practice.ReceivedText;
        Assert.DoesNotContain("_", revealed, "The copy box only ever holds ordinary spaces.");
        StringAssert.StartsWith(revealed, entry);
        var result = await app.Result();
        var vm = (PracticeResultWindowViewModel)result.DataContext!;
        Assert.AreEqual("0", vm.ErrorsText);
        Assert.IsTrue(vm.Rows.All(r => r.SentGroup == entry.Replace(' ', '\u00A0')));
        Assert.IsFalse(ScenarioDesktop.Controls<TextBlock>(result).Any(t => t.Text?.Contains('_') == true), "No underscore is ever shown.");
        await app.CloseResult(result);
    }, c =>
    {
        ArrangeTopicLists(c);
        c.Topics.Names = ["Little John"];
        c.Topics.Abbreviations = ["QSL  VIA BURO"];
        c.UiPreferences.RevealSentTextAfterPractice = true;
    });

    [TestMethod]
    public Task UserTypesAUnderscoreAndItCountsAsASpace() => Run(async app =>
    {
        SelectTopic(app, PracticeTopic.Cities);
        await app.Practice(null);
        var sent = app.Services.GetRequiredService<IPracticeController>().LastGeneratedText;

        // The copy uses underscores between the items as well: the same as ordinary ones.
        ScenarioDesktop.Text(app.Main, app.Main.FindControl<TextBox>("ReceivedTextBox")!, sent.Replace(' ', '_'));
        var result = await app.Result();
        Assert.AreEqual("0", ((PracticeResultWindowViewModel)result.DataContext!).ErrorsText);
        Assert.HasCount(PracticeItems.SplitItems(sent).Count, ((PracticeResultWindowViewModel)result.DataContext!).Rows);
        await app.CloseResult(result);
    }, c =>
    {
        ArrangeTopicLists(c);
        c.Topics.Cities = ["NEW YORK", "WIEN"];
    });

    [TestMethod]
    public Task UserEntersAMultiWordCityInSettingsWithAnyKindOfSpace() => Run(async app =>
    {
        var settings = await Settings(app);
        ScenarioDesktop.Tab(settings, ScenarioDesktop.TopicsTab);
        ScenarioDesktop.SubTab(settings, ScenarioDesktop.CitiesSubTab);
        ScenarioDesktop.Text(settings, ScenarioDesktop.Tip<TextBox>(settings, "City names"), "FRANKFURT   AM_MAIN\nDEN HAAG");
        await ApplySettings(app, settings);

        CollectionAssert.AreEqual(new[] { "DEN HAAG", "FRANKFURT AM MAIN" }, app.Configuration.Current.Topics.Cities,
            "Saved with ordinary single spaces.");
        SelectTopic(app, PracticeTopic.Cities);
        await app.Practice("@sent");
        var items = PracticeItems.SplitItems(app.Services.GetRequiredService<IPracticeController>().LastGeneratedText);
        Assert.IsTrue(items.All(i => i is "DEN_HAAG" or "FRANKFURT_AM_MAIN"), string.Join('|', items));
        var result = await app.Result();
        Assert.AreEqual("0", ((PracticeResultWindowViewModel)result.DataContext!).ErrorsText);
        await app.CloseResult(result);
    }, ArrangeTopicLists);

    [TestMethod]
    public Task UserCopiesCustomTextWordByWord() => Run(async app =>
    {
        await app.Practice("QSL VIA BURO");
        var result = await app.Result();
        var vm = (PracticeResultWindowViewModel)result.DataContext!;
        Assert.HasCount(3, vm.Rows, "The custom text stays word by word.");
        Assert.AreEqual("0", vm.ErrorsText);
        await app.CloseResult(result);
    }, c => c.Practice.CustomText = "QSL VIA BURO");
}
