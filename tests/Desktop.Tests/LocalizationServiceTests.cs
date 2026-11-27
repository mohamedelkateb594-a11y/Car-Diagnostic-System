using System.Windows;
using Diagnostic.Desktop.Services;
using Xunit;

namespace Desktop.Tests;

public class LocalizationServiceTests
{
    [Fact]
    public void Indexer_BeforeAnyLanguageIsLoaded_ReturnsTheKey()
    {
        var service = new LocalizationService();

        Assert.Equal("AppTitle", service["AppTitle"]);
    }

    [Fact]
    public void Use_Arabic_SetsRightToLeftAndReturnsLoadedString()
    {
        var service = new LocalizationService();
        var strings = new ResourceDictionary { ["AppTitle"] = "عنوان تجريبي" };

        service.Use(AppLanguage.Arabic, strings);

        Assert.Equal(AppLanguage.Arabic, service.Current);
        Assert.Equal(FlowDirection.RightToLeft, service.FlowDirection);
        Assert.Equal("عنوان تجريبي", service["AppTitle"]);
    }

    [Fact]
    public void Use_English_SetsLeftToRight()
    {
        var service = new LocalizationService();

        service.Use(AppLanguage.English, new ResourceDictionary());

        Assert.Equal(FlowDirection.LeftToRight, service.FlowDirection);
    }

    [Fact]
    public void Use_MissingKey_ReturnsTheKey()
    {
        var service = new LocalizationService();

        service.Use(AppLanguage.English, new ResourceDictionary { ["Known"] = "x" });

        Assert.Equal("Unknown", service["Unknown"]);
    }

    [Fact]
    public void Use_RaisesItemChangedAndIncrementsRevision()
    {
        var service = new LocalizationService();
        var raised = new List<string?>();
        service.PropertyChanged += (_, e) => raised.Add(e.PropertyName);

        service.Use(AppLanguage.Arabic, new ResourceDictionary());

        Assert.Contains("Item[]", raised);
        Assert.Contains(nameof(LocalizationService.FlowDirection), raised);
        Assert.Equal(1, service.Revision);
    }
}
