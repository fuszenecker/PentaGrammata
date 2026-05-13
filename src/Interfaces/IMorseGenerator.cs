namespace PentaGrammata.Services;

public interface IMorseGenerator
{
    string GenerateGroupsOf5(string characterPalette, int numberOfGroups);
}