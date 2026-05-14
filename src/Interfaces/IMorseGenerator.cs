namespace PentaGrammata.Services;

public interface IMorseGenerator
{
    string GenerateGroupsOf5(string characterSet, int numberOfGroups);
}