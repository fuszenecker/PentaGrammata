using Avalonia;
using Avalonia.Headless;
using Avalonia.Controls;
using Avalonia.LogicalTree;
using Avalonia.VisualTree;
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
        // 1 minute at 15 WPM is worth 10 groups of 5 characters, all played: callsigns, cities
        // and names twice by default, abbreviations once. Each item is checked once.
        int repetitions = topic == PracticeTopic.Abbreviations ? 1 : 2;
        Assert.IsGreaterThanOrEqualTo(50, repetitions * string.Concat(PracticeItems.SplitWords(sent)).Length, sent);
        Assert.DoesNotContain(PracticeItems.RepeatJoiner.ToString(), sent);

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
    public Task UserShufflesTheLinesOfTheCustomText() => Run(async app =>
    {
        var settings = await Settings(app);
        var box = ScenarioDesktop.CustomText(settings);
        var shuffle = ScenarioDesktop.Tip<CheckBox>(settings, "Send the lines of the custom text in a random order");
        Assert.AreEqual("Shuffle lines", shuffle.Content);
        ScenarioDesktop.Text(settings, box, "ONE\nTWO\nTHREE\nFOUR\nFIVE\nSIX\nSEVEN\nEIGHT\nNINE\nNOVI SAD");
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
            // The lines are shuffled; NOVI SAD stays one item.
            CollectionAssert.AreEquivalent("ONE TWO THREE FOUR FIVE SIX SEVEN EIGHT NINE NOVI_SAD".Split(' '), order.Split(' '));
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
        ScenarioDesktop.Click(settings, ScenarioDesktop.Tip<CheckBox>(settings, "Send the lines of the custom text in a random order"));
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
        Assert.IsFalse(ScenarioDesktop.Tip<CheckBox>(settings, "Send the lines of the custom text in a random order").IsChecked);
        ScenarioDesktop.Text(settings, box, "ONE\nTWO\nTHREE FOUR\nFIVE\nSIX");
        await ApplySettings(app, settings);

        for (int i = 0; i < 3; i++)
        {
            await app.Practice(null, stop: true);
            Assert.AreEqual("ONE TWO THREE_FOUR FIVE SIX", app.Services.GetRequiredService<IPracticeController>().LastGeneratedText);
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
    public Task UserCopiesALineOfTheCustomTextAsOneRow_ScoredWordByWord() => Run(async app =>
    {
        await app.Practice("QSL VIA BURX");
        var result = await app.Result();
        var vm = (PracticeResultWindowViewModel)result.DataContext!;
        Assert.HasCount(1, vm.Rows, "One line of the custom text is one row, like a multi-word city.");
        Assert.AreEqual("QSL\u00A0VIA\u00A0BURO", vm.Rows[0].SentGroup);
        Assert.AreEqual("QSL\u00A0VIA\u00A0BURX", vm.Rows[0].ReceivedGroup);
        Assert.AreEqual("1", vm.ErrorsText, "Scored word by word: one wrong letter.");
        Assert.AreEqual("10", vm.CharacterCountText, "The spaces are not characters.");
        result.UpdateLayout();
        foreach (var block in ScenarioDesktop.Controls<TextBlock>(result))
            Assert.DoesNotContain("_", block.Text ?? string.Empty, block.Text);
        await app.CloseResult(result);
    }, c => c.Practice.CustomText = "QSL VIA BURO");

    [TestMethod]
    public Task UserRepeatsEachNameAsInAQso() => Run(async app =>
    {
        var settings = await Settings(app);
        ScenarioDesktop.Tab(settings, ScenarioDesktop.TopicsTab);
        ScenarioDesktop.SubTab(settings, ScenarioDesktop.AbbreviationsSubTab);
        var (abbreviationsMinimum, abbreviationsMaximum) = ScenarioDesktop.RepeatRange(settings);
        Assert.AreEqual(1m, abbreviationsMinimum.Value, "Abbreviations are sent once by default.");
        Assert.AreEqual(1m, abbreviationsMaximum.Value);
        ScenarioDesktop.SubTab(settings, ScenarioDesktop.NamesSubTab);
        var (minimum, maximum) = ScenarioDesktop.RepeatRange(settings);
        Assert.AreEqual(2m, minimum.Value, "Names are sent twice by default.");
        Assert.AreEqual(2m, maximum.Value);
        minimum.Value = 3;
        Assert.AreEqual(3m, maximum.Value, "Raising the minimum raises the maximum.");
        await ApplySettings(app, settings);
        Assert.AreEqual(3, app.Configuration.Current.Topics.Repetitions.Names.Minimum);
        Assert.AreEqual(3, app.Configuration.Current.Topics.Repetitions.Names.Maximum);

        SelectTopic(app, PracticeTopic.Names);
        await app.Practice("@sent");
        var items = app.Services.GetRequiredService<IPracticeController>().LastGeneratedText.Split(' ');
        Assert.IsTrue(items.All(i => i == "LACI"), string.Join(' ', items));
        var result = await app.Result();
        var view = (PracticeResultWindowViewModel)result.DataContext!;
        Assert.AreEqual("0", view.ErrorsText, "Each name copied once is a perfect copy.");
        Assert.HasCount(items.Length, view.Rows, "Each name is one row, not three.");
        await app.CloseResult(result);

        await app.Configuration.FlushAsync();
        var reloaded = app.Services.GetRequiredService<IConfigurationStore>().Load();
        Assert.AreEqual(3, reloaded.Topics.Repetitions.Names.Minimum);
        Assert.AreEqual(2, reloaded.Topics.Repetitions.Cities.Maximum, "The other topics keep their defaults.");
    }, ArrangeTopicLists);

    [TestMethod]
    public Task UserHearsEachCityTwice_ButSeesAndCopiesItOnce() => Run(async app =>
    {
        SelectTopic(app, PracticeTopic.Cities);
        await app.Practice("@sent");
        var sent = app.Services.GetRequiredService<IPracticeController>().LastGeneratedText;
        Assert.DoesNotContain(PracticeItems.RepeatJoiner.ToString(), sent);
        var items = PracticeItems.SplitItems(sent);
        Assert.IsTrue(items.All(i => i is "WIEN" or "CAPE_TOWN"), sent);

        var result = await app.Result();
        var view = (PracticeResultWindowViewModel)result.DataContext!;
        Assert.AreEqual("0", view.ErrorsText);
        Assert.HasCount(items.Count, view.Rows, "One row per city, its repetition is not checked.");
        result.UpdateLayout();
        foreach (var block in ScenarioDesktop.Controls<TextBlock>(result))
        {
            Assert.DoesNotContain("_", block.Text ?? string.Empty, block.Text);
            Assert.DoesNotContain("|", block.Text ?? string.Empty, block.Text);
        }

        await app.CloseResult(result);
        foreach (var block in ScenarioDesktop.Controls<TextBlock>(app.Main))
            Assert.DoesNotContain("|", block.Text ?? string.Empty, block.Text);
        foreach (var box in ScenarioDesktop.Controls<TextBox>(app.Main))
            Assert.DoesNotContain("|", box.Text ?? string.Empty, "The revealed and copied text shows each city once.");
    }, ArrangeTopicLists);

    [TestMethod]
    [DataRow(ScenarioDesktop.EmeaCallsignsSubTab, 2)]
    [DataRow(ScenarioDesktop.GlobalCallsignsSubTab, 2)]
    [DataRow(ScenarioDesktop.CitiesSubTab, 2)]
    [DataRow(ScenarioDesktop.NamesSubTab, 2)]
    [DataRow(ScenarioDesktop.AbbreviationsSubTab, 1)]
    [DataRow(ScenarioDesktop.CustomTextSubTab, 1)]
    public Task UserSeesTheRepeatRangeAboveEveryTopicList(int subTab, int expected) => Run(async app =>
    {
        var settings = await Settings(app);
        ScenarioDesktop.Tab(settings, ScenarioDesktop.TopicsTab);
        ScenarioDesktop.SubTab(settings, subTab);
        var (minimum, maximum) = ScenarioDesktop.RepeatRange(settings);

        Assert.AreEqual((decimal)expected, minimum.Value);
        Assert.AreEqual((decimal)expected, maximum.Value);
        Assert.IsTrue(minimum.IsEffectivelyVisible && minimum.IsEffectivelyEnabled);
        Assert.IsTrue(maximum.IsEffectivelyVisible && maximum.IsEffectivelyEnabled);
        Assert.AreEqual(1m, minimum.Minimum);
        Assert.AreEqual((decimal)Repetition.Limit, minimum.Maximum);
        Assert.AreEqual((decimal)Repetition.Limit, maximum.Maximum);
        var labels = ScenarioDesktop.Controls<TextBlock>(settings)
            .Where(t => ReferenceEquals(t.FindLogicalAncestorOfType<ContentControl>(), minimum.FindLogicalAncestorOfType<ContentControl>()))
            .Select(t => t.Text).ToArray();
        CollectionAssert.IsSubsetOf(new[] { "Repeat", "to", "times" }, labels);
        ScenarioDesktop.Click(settings, "Cancel");
    }, ArrangeTopicLists);

    [TestMethod]
    public Task UserLowersTheMaximumBelowTheMinimum_AndTheMinimumFollows() => Run(async app =>
    {
        var settings = await Settings(app);
        ScenarioDesktop.Tab(settings, ScenarioDesktop.TopicsTab);
        ScenarioDesktop.SubTab(settings, ScenarioDesktop.CitiesSubTab);
        var (minimum, maximum) = ScenarioDesktop.RepeatRange(settings);
        maximum.Value = 1;
        Assert.AreEqual(1m, minimum.Value);

        maximum.Value = 4;
        minimum.Value = 2;
        Assert.AreEqual(4m, maximum.Value, "A minimum below the maximum leaves the maximum alone.");
        await ApplySettings(app, settings);

        Assert.AreEqual(2, app.Configuration.Current.Topics.Repetitions.Cities.Minimum);
        Assert.AreEqual(4, app.Configuration.Current.Topics.Repetitions.Cities.Maximum);
    }, ArrangeTopicLists);

    [TestMethod]
    public Task UserCancelsARepetitionChange_AndNothingChanges() => Run(async app =>
    {
        var settings = await Settings(app);
        ScenarioDesktop.Tab(settings, ScenarioDesktop.TopicsTab);
        ScenarioDesktop.SubTab(settings, ScenarioDesktop.NamesSubTab);
        ScenarioDesktop.RepeatRange(settings).Maximum.Value = 5;
        ScenarioDesktop.Click(settings, "Cancel");
        await ScenarioDesktop.Until(() => !app.ViewModel.OpenSettingsCommand.IsRunning, "close the settings dialog");

        Assert.AreEqual(2, app.Configuration.Current.Topics.Repetitions.Names.Maximum);
        settings = await Settings(app);
        ScenarioDesktop.Tab(settings, ScenarioDesktop.TopicsTab);
        ScenarioDesktop.SubTab(settings, ScenarioDesktop.NamesSubTab);
        Assert.AreEqual(2m, ScenarioDesktop.RepeatRange(settings).Maximum.Value, "Reopened, the dialog shows the saved range.");
        ScenarioDesktop.Click(settings, "Cancel");
    }, ArrangeTopicLists);

    [TestMethod]
    public Task UserRepeatsEachLineOfTheCustomTextAsAWhole() => Run(async app =>
    {
        var settings = await Settings(app);
        ScenarioDesktop.Text(settings, ScenarioDesktop.CustomText(settings), "NOVI SAD\nCQ DE HA5XYZ");
        await ApplySettings(app, settings);
        SelectTopic(app, PracticeTopic.CustomText);
        await app.Practice("@sent");
        int once = app.Audio.Samples.Length;
        var first = await app.Result();
        await app.CloseResult(first);

        settings = await Settings(app);
        ScenarioDesktop.CustomText(settings);
        ScenarioDesktop.RepeatRange(settings).Minimum.Value = 3;
        await ApplySettings(app, settings);
        Assert.AreEqual(3, app.Configuration.Current.Topics.Repetitions.CustomText.Maximum);

        await app.Practice("@sent");
        Assert.AreEqual("NOVI_SAD CQ_DE_HA5XYZ", app.Services.GetRequiredService<IPracticeController>().LastGeneratedText, "Each line checked once.");
        // vvv = and <ar> are played once either way; the two lines three times each.
        Assert.IsGreaterThan(once * 2, app.Audio.Samples.Length, "Every repetition is played.");
        foreach (var box in ScenarioDesktop.Controls<TextBox>(app.Main))
        {
            Assert.DoesNotContain("_", box.Text ?? string.Empty);
            Assert.DoesNotContain("|", box.Text ?? string.Empty);
        }

        var result = await app.Result();
        var view = (PracticeResultWindowViewModel)result.DataContext!;
        Assert.AreEqual("0", view.ErrorsText);
        Assert.HasCount(2, view.Rows, "One row per line.");
        Assert.AreEqual("NOVI\u00A0SAD", view.Rows[0].SentGroup);
        Assert.AreEqual("CQ\u00A0DE\u00A0HA5XYZ", view.Rows[1].SentGroup);
        await app.CloseResult(result);
    }, ArrangeTopicLists);

    [TestMethod]
    public Task UserCopiesARepeatedNameOnce_AndThatIsAPerfectCopy() => Run(async app =>
    {
        SelectTopic(app, PracticeTopic.Names);
        await app.Practice("@sent");

        var sent = app.Services.GetRequiredService<IPracticeController>().LastGeneratedText;
        Assert.IsTrue(sent.Split(' ').All(i => i == "LACI"), sent);
        var result = await app.Result();
        var view = (PracticeResultWindowViewModel)result.DataContext!;
        Assert.AreEqual("0", view.ErrorsText, "Heard twice, copied once: no error.");
        Assert.AreEqual(sent.Split(' ').Length * 4, int.Parse(view.CharacterCountText, System.Globalization.CultureInfo.InvariantCulture),
            "Only one copy of each name is counted.");
        await app.CloseResult(result);
    }, ArrangeTopicLists);

    [TestMethod]
    public Task UserMixesTopics_AndEachItemIsRepeatedByItsOwnTopic() => Run(async app =>
    {
        SelectTopic(app, PracticeTopic.Mixed);
        await app.Practice("@sent");

        var sent = app.Services.GetRequiredService<IPracticeController>().LastGeneratedText;
        Assert.DoesNotContain(PracticeItems.RepeatJoiner.ToString(), sent);
        var items = sent.Split(' ');
        Assert.Contains("LACI", items);
        var result = await app.Result();
        var view = (PracticeResultWindowViewModel)result.DataContext!;
        Assert.AreEqual("0", view.ErrorsText);
        Assert.HasCount(items.Length, view.Rows);
        await app.CloseResult(result);
    }, c =>
    {
        ArrangeTopicLists(c);
        c.Topics.CallsignPatternsEmea = [];
        c.Topics.CallsignPatternsGlobal = [];
        c.Topics.Cities = [];
        c.Topics.Abbreviations = [];
        c.Practice.CustomText = string.Empty;
        c.Topics.Repetitions.Names = new Repetition(3, 3);
        c.Practice.DefaultDurationMins = 3;
    });

    [TestMethod]
    [DataRow(ScenarioDesktop.EmeaCallsignsSubTab)]
    [DataRow(ScenarioDesktop.GlobalCallsignsSubTab)]
    [DataRow(ScenarioDesktop.CitiesSubTab)]
    [DataRow(ScenarioDesktop.NamesSubTab)]
    [DataRow(ScenarioDesktop.AbbreviationsSubTab)]
    [DataRow(ScenarioDesktop.CustomTextSubTab)]
    public Task UserFindsTheRepeatRangeBelowTheListAndAsWideAsIt(int subTab) => Run(async app =>
    {
        var settings = await Settings(app);
        ScenarioDesktop.Tab(settings, ScenarioDesktop.TopicsTab);
        ScenarioDesktop.SubTab(settings, subTab);
        var (minimum, maximum) = ScenarioDesktop.RepeatRange(settings);
        var selected = minimum.FindLogicalAncestorOfType<TabItem>()!;
        var box = ScenarioDesktop.Controls<TextBox>(settings).Single(t => ReferenceEquals(t.FindLogicalAncestorOfType<TabItem>(), selected) && t.IsEffectivelyVisible
            && !t.GetVisualAncestors().OfType<NumericUpDown>().Any());
        var row = (Control)minimum.FindLogicalAncestorOfType<ContentControl>()!;
        Rect Bounds(Control c) => new(c.TranslatePoint(new Point(0, 0), settings)!.Value, c.Bounds.Size);
        var boxBounds = Bounds(box);
        var rowBounds = Bounds(row);
        var endBounds = rowBounds;
        var minimumBounds = Bounds(minimum);
        var maximumBounds = Bounds(maximum);

        Assert.IsGreaterThanOrEqualTo(boxBounds.Bottom, rowBounds.Top, "The repeat range sits below the list.");
        Assert.IsLessThanOrEqualTo(12d, rowBounds.Top - boxBounds.Bottom, "...right below it.");
        Assert.AreEqual(boxBounds.Left, rowBounds.Left, 0.5, "The row starts where the list starts.");
        Assert.AreEqual(boxBounds.Right, endBounds.Right, 1.0, "The row ends where the list ends.");
        Assert.AreEqual(minimumBounds.Width, maximumBounds.Width, 0.5, "The two counters are the same width.");
        Assert.AreEqual(minimumBounds.Center.Y, maximumBounds.Center.Y, 0.5, "The two counters are on one line.");
        Assert.IsGreaterThanOrEqualTo(80d, minimumBounds.Width, "The counters fill the row instead of shrinking.");
        Assert.IsLessThanOrEqualTo(settings.Bounds.Height, endBounds.Bottom, "The row is inside the dialog.");
        if (subTab == ScenarioDesktop.CustomTextSubTab)
        {
            // The shuffle checkbox has a line of its own below the repeat range, aligned with the text.
            var shuffle = Bounds(ScenarioDesktop.Tip<CheckBox>(settings, "Send the lines of the custom text"));
            Assert.IsGreaterThanOrEqualTo(rowBounds.Bottom, shuffle.Top);
            Assert.AreEqual(boxBounds.Left, shuffle.Left, 0.5);
            Assert.IsLessThanOrEqualTo(settings.Bounds.Height, shuffle.Bottom, "The checkbox is inside the dialog.");
        }

        using (var frame = settings.CaptureRenderedFrame()) Assert.IsNotNull(frame);
        ScenarioDesktop.Click(settings, "Cancel");
    }, ArrangeTopicLists);
}
