using System.Collections.ObjectModel;
using System.Globalization;

using PentaGrammata.Models;

namespace PentaGrammata.ViewModels;

public sealed class PracticeResultWindowViewModel : ViewModelBase
{
    public ObservableCollection<PracticeResultRow> Rows { get; }

    public string CharacterCountText { get; }
    public string ErrorsText { get; }
    public string ErrorRateText { get; }

    public PracticeResultWindowViewModel(PracticeResult result)
    {
        Rows = new ObservableCollection<PracticeResultRow>(result.Rows);

        CharacterCountText = result.CharacterCount.ToString(CultureInfo.InvariantCulture);
        ErrorsText = result.ErrorCount.ToString(CultureInfo.InvariantCulture);
        ErrorRateText = $"{result.ErrorRatePercent:F2}%";
    }
}
