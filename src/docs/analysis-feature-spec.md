# Analysis menu feature notes

## Menu structure

Add a new top-level menu named **Analysis** between **Settings** and **Help**.

Submenu items:
- Trends
- Confusions

Both submenu items open their own dialog windows.

Suggested display naming in UI:
- Trends dialog title: **Performance Trends**
- Confusions dialog title: **Confusion Matrix**

## Trends dialog requirements

The dialog should show a time-series chart with:
- Character speed (WPM)
- Average speed (WPM)
- Error rate (%)
- Error limit (%)
- Noise value (dB)

Interaction requirements:
- Time-axis zooming
- Time-axis panning/navigation

## Confusions dialog requirements

The dialog should show a confusion matrix where:
- Vertical axis = expected character
- Horizontal axis = received character

Scoring requirements:
- Older observations should have less visual weight than recent ones
- Rare observations should be less emphasized than frequent ones

Distance/alignment requirement:
- Build confusions from Levenshtein alignment so substitutions, insertions, and deletions are represented consistently
