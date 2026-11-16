using SmallestAgent.Shared.Services;
using Windows.Storage;
using Windows.Storage.Pickers;
namespace SmallestAgent.Maui;
public sealed class WindowsHostActions : IHostActions
{
    public Task CopyAsync(string text) => Clipboard.Default.SetTextAsync(text);
    public async Task<string> SaveAsync(string filename, string mime, string text)
    {
        var picker = new FileSavePicker { SuggestedFileName = Path.GetFileNameWithoutExtension(filename) };
        picker.FileTypeChoices.Add("Lesson export", [Path.GetExtension(filename)]);
        var window = (Microsoft.UI.Xaml.Window)Application.Current!.Windows[0].Handler!.PlatformView!;
        WinRT.Interop.InitializeWithWindow.Initialize(picker, WinRT.Interop.WindowNative.GetWindowHandle(window));
        var file = await picker.PickSaveFileAsync();
        if (file is null) return "Save cancelled.";
        await FileIO.WriteTextAsync(file, text);
        return $"Saved {file.Name}.";
    }
}
